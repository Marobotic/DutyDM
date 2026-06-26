using System;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace DutyDM
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        // ── Discord ───────────────────────────────────────────────
        /// <summary>The user's unique Discord @username (not their display name).</summary>
        public string DiscordUsername { get; set; } = "";

        // ── Notifications (independent toggles) ───────────────────
        /// <summary>DM me when a Duty Finder queue pops.</summary>
        public bool Enable { get; set; } = false;

        /// <summary>DM me when my party fills to 8/8 (regular or cross-world).</summary>
        public bool PartyFullEnable { get; set; } = false;

        public event Action<Configuration>? OnSaved;

        [NonSerialized]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pi) => pluginInterface = pi;

        public void Save()
        {
            pluginInterface?.SavePluginConfig(this);
            OnSaved?.Invoke(this);
        }
    }
}
