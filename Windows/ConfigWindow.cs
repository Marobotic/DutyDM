using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using DutyDM.PushChannels;
using DutyDM.Services;

namespace DutyDM.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private const string InviteUrl = "https://discord.gg/Z4scjq8bdK";
        private const string KofiUrl = "https://ko-fi.com/marobotic";

        private readonly Configuration config;
        private readonly PushService pushService;
        private readonly IPluginLog pluginLog;
        private readonly Action<PushResult, string> onResult;

        private string username = string.Empty;
        private bool dutyPopEnable;
        private bool partyFullEnable;
        private bool testing;
        private string statusText = string.Empty;
        private bool statusOk;

        private int pushedColors;

        public ConfigWindow(Configuration config, PushService pushService, IPluginLog pluginLog, Action<PushResult, string> onResult)
            : base("DutyDM###DutyDMConfig", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar)
        {
            this.config = config;
            this.pushService = pushService;
            this.pluginLog = pluginLog;
            this.onResult = onResult;

            Size = new Vector2(430, 0);
            SizeCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(390, 250),
                MaximumSize = new Vector2(640, 600),
            };

            LoadFromConfig();
        }

        public void Dispose() { }

        private void LoadFromConfig()
        {
            username = config.DiscordUsername;
            dutyPopEnable = config.Enable;
            partyFullEnable = config.PartyFullEnable;
        }

        public override void OnOpen() => LoadFromConfig();

        public override void PreDraw()
        {
            pushedColors = Theme.PushColors();
            ImGui.PushStyleColor(ImGuiCol.WindowBg, Theme.BgOuter);
            ImGui.PushStyleColor(ImGuiCol.Border, Theme.BorderDefault);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16, 14));
        }

        public override void PostDraw()
        {
            ImGui.PopStyleVar(4);
            ImGui.PopStyleColor(2 + pushedColors);
        }

        public override void Draw()
        {
            Theme.SectionLabel("DISCORD ALERTS");
            ImGui.TextColored(Theme.TextSecondary, "Get a Discord DM for the events you pick below.");
            ImGui.Dummy(new Vector2(0, 8));

            // Step 1 - join the server
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(Theme.TextPrimary, "1.  Join the Discord server");
            ImGui.SameLine();
            float btnX = ImGui.GetContentRegionAvail().X - 64;
            if (btnX > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + btnX);
            if (Theme.SecondaryButton("Join##invite", new Vector2(64, 0)))
                OpenUrl(InviteUrl);

            ImGui.Dummy(new Vector2(0, 6));

            // Step 2 - username
            ImGui.TextColored(Theme.TextPrimary, "2.  Your Discord username");
            ImGui.Dummy(new Vector2(0, 2));
            Theme.InputField("##username", "e.g. mayobotic", ref username, 64);
            ImGui.TextColored(Theme.TextMuted, "Your unique @username - not your display name.");
            ImGui.Dummy(new Vector2(0, 8));

            // Step 3 - pick alerts (each is independent)
            ImGui.TextColored(Theme.TextPrimary, "3.  Pick your alerts");
            ImGui.Dummy(new Vector2(0, 2));
            if (Theme.Checkbox("DM me when my duty pops", ref dutyPopEnable))
            {
                config.Enable = dutyPopEnable;
                config.Save();
            }
            if (Theme.Checkbox("DM me when my party fills (8/8)", ref partyFullEnable))
            {
                config.PartyFullEnable = partyFullEnable;
                config.Save();
            }
            if (partyFullEnable)
                ImGui.TextColored(Theme.TextMuted, "     Skipped while you're already in a duty.");

            ImGui.Dummy(new Vector2(0, 10));
            ImGui.Separator();
            ImGui.Dummy(new Vector2(0, 6));

            // Action buttons
            float full = ImGui.GetContentRegionAvail().X;
            float half = (full - 8) / 2f;
            if (Theme.PrimaryButton("Save", new Vector2(half, 30)))
            {
                SaveConfig();
                statusText = "Saved.";
                statusOk = true;
            }
            ImGui.SameLine(0, 8);
            using (ImGuiDisabled(testing))
            {
                if (Theme.SecondaryButton(testing ? "Sending..." : "Send test DM", new Vector2(half, 30)))
                    RunTest();
            }

            // Status line
            if (!string.IsNullOrEmpty(statusText))
            {
                ImGui.Dummy(new Vector2(0, 6));
                ImGui.PushTextWrapPos(0);
                ImGui.TextColored(statusOk ? Theme.AccentGreen : Theme.AccentRed, statusText);
                ImGui.PopTextWrapPos();
            }

            // Footer - a little Ko-fi love
            ImGui.Dummy(new Vector2(0, 10));
            ImGui.Separator();
            ImGui.Dummy(new Vector2(0, 6));
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(Theme.TextMuted, "Enjoying DutyDM?");
            ImGui.SameLine();
            float kofiX = ImGui.GetContentRegionAvail().X - Theme.KofiButtonWidth("Support me on Ko-fi");
            if (kofiX > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + kofiX);
            if (Theme.KofiButton("Support me on Ko-fi"))
                OpenUrl(KofiUrl);
        }

        private void SaveConfig()
        {
            config.DiscordUsername = username.Trim();
            config.Enable = dutyPopEnable;
            config.PartyFullEnable = partyFullEnable;
            config.Save();
        }

        private async void RunTest()
        {
            if (testing) return;
            SaveConfig();
            testing = true;
            statusText = "Sending test DM...";
            statusOk = true;
            try
            {
                var result = await pushService.SendNotificationAsync("DutyDM test", "If you got this, your DMs are working!");
                statusOk = result.Ok;
                statusText = result.Ok ? "Test DM sent - check your Discord!" : result.Error ?? "Failed.";
                onResult(result, "Test");
            }
            catch (Exception ex)
            {
                statusOk = false;
                statusText = ex.Message;
                pluginLog.Error($"Test failed: {ex.Message}");
            }
            finally
            {
                testing = false;
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                // ignored - opening a browser is best-effort
            }
        }

        /// <summary>RAII helper so BeginDisabled/EndDisabled always balance.</summary>
        private static DisabledScope ImGuiDisabled(bool disabled) => new(disabled);

        private readonly struct DisabledScope : IDisposable
        {
            private readonly bool active;
            public DisabledScope(bool active)
            {
                this.active = active;
                if (active) ImGui.BeginDisabled();
            }
            public void Dispose()
            {
                if (active) ImGui.EndDisabled();
            }
        }
    }
}
