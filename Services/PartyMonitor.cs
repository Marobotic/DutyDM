using System;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using DutyDM.PushChannels;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace DutyDM.Services
{
    /// <summary>
    /// Watches the party each frame and DMs the user the moment it fills to 8/8 by
    /// gradually growing - i.e. the last seat is taken while you were waiting.
    /// Handles both regular parties (IPartyList) and cross-world parties
    /// (InfoProxyCrossRealm).
    /// Only the incremental 7 -> 8 transition counts as "the party just filled". A jump
    /// straight to 8 is deliberately ignored, because that's not a moment worth a DM:
    ///   - joining a PF that's already 7/8 (your size goes 1 -> 8 in one step), or
    ///   - re-forming after a duty (members blink out and back, snapping 1 -> 8).
    /// Alerts are also suppressed while you're inside a duty - a duty party is always
    /// 8/8, so notifying there is pointless. State is tracked silently throughout so the
    /// edge detection stays correct.
    /// </summary>
    public class PartyMonitor : IDisposable
    {
        private const int FullPartySize = 8;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(500);

        private readonly IFramework framework;
        private readonly IPartyList partyList;
        private readonly ICondition condition;
        private readonly IPluginLog pluginLog;
        private readonly PushService pushService;
        private readonly Func<bool> getEnableState;
        private readonly Action<PushResult, string> onResult;

        private bool subscribed;
        private bool disposed;
        private int lastSize;
        private TimeSpan sinceLastCheck;

        public PartyMonitor(
            Func<bool> getEnableState,
            IFramework framework,
            IPartyList partyList,
            ICondition condition,
            IPluginLog pluginLog,
            PushService pushService,
            Action<PushResult, string> onResult)
        {
            this.getEnableState = getEnableState;
            this.framework = framework;
            this.partyList = partyList;
            this.condition = condition;
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
            // Seed from the current size so enabling while already full (or jumping
            // straight to full next tick) doesn't fire.
            lastSize = GetPartySize();
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
            int prevSize = lastSize;
            lastSize = size;

            // Only an incremental fill - the last seat being taken (7 -> 8) - counts.
            // A jump straight to full (joining a ready PF, re-forming after a duty) is
            // not a moment worth a DM, so we ignore anything where the previous size
            // wasn't exactly one short of full.
            bool justFilled = size >= FullPartySize && prevSize == FullPartySize - 1;
            if (!justFilled) return;

            if (IsBoundByDuty())
            {
                // In a duty the party is always 8/8, so the alert would just be noise.
                pluginLog.Info($"DutyDM: party full ({size}/{FullPartySize}) but in a duty - skipping alert.");
            }
            else
            {
                pluginLog.Info($"DutyDM: party just filled ({prevSize} -> {size}/{FullPartySize}).");
                _ = SendPartyFullAsync(size);
            }
        }

        /// <summary>True while inside an instanced duty (Duty Finder content, trials, raids, etc.).</summary>
        private bool IsBoundByDuty()
            => condition[ConditionFlag.BoundByDuty]
            || condition[ConditionFlag.BoundByDuty56]
            || condition[ConditionFlag.BoundByDuty95];

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
