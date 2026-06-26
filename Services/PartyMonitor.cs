using System;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using DutyDM.PushChannels;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace DutyDM.Services
{
    /// <summary>
    /// Watches the party each frame and DMs the user the moment it fills to 8/8.
    /// Handles both regular parties (IPartyList) and cross-world parties
    /// (InfoProxyCrossRealm). Only fires on the rising edge to full, so joins, leaves,
    /// kicks and disbands before that are tracked silently.
    /// </summary>
    public class PartyMonitor : IDisposable
    {
        private const int FullPartySize = 8;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(500);

        private readonly IFramework framework;
        private readonly IPartyList partyList;
        private readonly IPluginLog pluginLog;
        private readonly PushService pushService;
        private readonly Func<bool> getEnableState;
        private readonly Action<PushResult, string> onResult;

        private bool subscribed;
        private bool disposed;
        private bool wasFull;
        private TimeSpan sinceLastCheck;

        public PartyMonitor(
            Func<bool> getEnableState,
            IFramework framework,
            IPartyList partyList,
            IPluginLog pluginLog,
            PushService pushService,
            Action<PushResult, string> onResult)
        {
            this.getEnableState = getEnableState;
            this.framework = framework;
            this.partyList = partyList;
            this.pluginLog = pluginLog;
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
            // Seed from the current state so enabling while already full doesn't fire.
            wasFull = GetPartySize() >= FullPartySize;
            sinceLastCheck = TimeSpan.Zero;
            framework.Update += OnFrameworkUpdate;
            subscribed = true;
            pluginLog.Info("DutyDM: party monitor on.");
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            framework.Update -= OnFrameworkUpdate;
            subscribed = false;
            pluginLog.Info("DutyDM: party monitor off.");
        }

        private void OnFrameworkUpdate(IFramework fw)
        {
            if (disposed) return;

            // Throttle: party state only needs checking a couple times a second.
            sinceLastCheck += fw.UpdateDelta;
            if (sinceLastCheck < CheckInterval) return;
            sinceLastCheck = TimeSpan.Zero;

            int size = GetPartySize();
            bool full = size >= FullPartySize;

            if (full && !wasFull)
            {
                pluginLog.Info($"DutyDM: party is full ({size}/{FullPartySize}).");
                _ = SendPartyFullAsync(size);
            }

            wasFull = full;
        }

        private async Task SendPartyFullAsync(int size)
        {
            var result = await pushService.SendNotificationAsync(
                "Your party is full!",
                $"All {FullPartySize} slots are filled ({size}/{FullPartySize}). Time to go!").ConfigureAwait(false);

            try { onResult(result, $"Party full ({size}/{FullPartySize})"); }
            catch (Exception ex) { pluginLog.Error($"DutyDM notify error: {ex.Message}"); }
        }

        /// <summary>Members in the current party (including you): cross-world if applicable, else regular.</summary>
        private unsafe int GetPartySize()
        {
            try
            {
                var crossRealm = InfoProxyCrossRealm.Instance();
                if (crossRealm != null && crossRealm->IsCrossRealm)
                    return InfoProxyCrossRealm.GetPartyMemberCount();
            }
            catch (Exception ex)
            {
                pluginLog.Error($"DutyDM: cross-realm read failed: {ex.Message}");
            }

            return partyList.Length;
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
