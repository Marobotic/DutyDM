using System;
using Dalamud.Plugin.Services;
using DutyDM.PushChannels;

namespace DutyDM.Services
{
    /// <summary>
    /// Subscribes to Dalamud's CfPop event (fires the instant a Duty Finder queue pops)
    /// and DMs the user via the bot, reporting the outcome back to the plugin.
    /// </summary>
    public class DutyListener : IDisposable
    {
        private readonly IClientState clientState;
        private readonly PushService pushService;
        private readonly IPluginLog pluginLog;
        private readonly Func<bool> getEnableState;
        private readonly Action<PushResult, string> onResult;

        private bool subscribed;
        private bool disposed;

        public DutyListener(
            Func<bool> getEnableState,
            IPluginLog pluginLog,
            IClientState clientState,
            PushService pushService,
            Action<PushResult, string> onResult)
        {
            this.getEnableState = getEnableState;
            this.pluginLog = pluginLog;
            this.clientState = clientState;
            this.pushService = pushService;
            this.onResult = onResult;
        }

        public void UpdateSubscriptionState()
        {
            if (disposed) return;
            if (getEnableState())
                Subscribe();
            else
                Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            clientState.CfPop += OnDutyPop;
            subscribed = true;
            pluginLog.Info("DutyDM: listening for duty pops.");
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            clientState.CfPop -= OnDutyPop;
            subscribed = false;
            pluginLog.Info("DutyDM: stopped listening for duty pops.");
        }

        private async void OnDutyPop(Lumina.Excel.Sheets.ContentFinderCondition condition)
        {
            if (disposed) return;

            string dutyName;
            try { dutyName = condition.Name.ToString(); }
            catch { dutyName = string.Empty; }

            pluginLog.Info($"DutyDM: CfPop triggered ({dutyName}).");

            const string title = "Your duty is ready!";
            string message = string.IsNullOrEmpty(dutyName)
                ? "Your queue popped - get back in game and commence!"
                : $"**{dutyName}** is ready - get back in game and commence!";

            var result = await pushService.SendNotificationAsync(title, message).ConfigureAwait(false);

            try { onResult(result, string.IsNullOrEmpty(dutyName) ? "Duty pop" : dutyName); }
            catch (Exception ex) { pluginLog.Error($"DutyDM notify error: {ex.Message}"); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Unsubscribe();
            GC.SuppressFinalize(this);
        }
    }
}
