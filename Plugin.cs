using System;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DutyDM.PushChannels;
using DutyDM.Services;
using DutyDM.Windows;

namespace DutyDM
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "DutyDM";

        private const string CommandMain = "/dutydm";
        private const string CommandDd = "/dd";
        private const string CommandDdm = "/ddm";

        private readonly IDalamudPluginInterface pluginInterface;
        private readonly ICommandManager commandManager;
        private readonly IPluginLog pluginLog;
        private readonly INotificationManager notificationManager;

        public readonly WindowSystem WindowSystem = new("DutyDM");
        private readonly ConfigWindow configWindow;

        public Configuration Configuration { get; }
        private readonly PushService pushService;
        private readonly DutyListener dutyListener;
        private readonly PartyMonitor partyMonitor;

        private IDtrBarEntry? dtrEntry;

        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IPluginLog pluginLog,
            INotificationManager notificationManager,
            IClientState clientState,
            IFramework framework,
            IPartyList partyList,
            ICondition condition,
            IDtrBar dtrBar)
        {
            this.pluginInterface = pluginInterface;
            this.commandManager = commandManager;
            this.pluginLog = pluginLog;
            this.notificationManager = notificationManager;

            Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(pluginInterface);
            Configuration.OnSaved += OnConfigurationSaved;

            pushService = new PushService(Configuration, pluginLog);
            dutyListener = new DutyListener(() => Configuration.Enable, pluginLog, clientState, pushService, OnPushResult);
            partyMonitor = new PartyMonitor(() => Configuration.PartyFullEnable, framework, partyList, condition, pluginLog, pushService, OnPushResult);
            dutyListener.UpdateSubscriptionState();
            partyMonitor.UpdateSubscriptionState();

            configWindow = new ConfigWindow(Configuration, pushService, pluginLog, OnPushResult);
            WindowSystem.AddWindow(configWindow);

            commandManager.AddHandler(CommandMain, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open DutyDM settings.",
            });
            commandManager.AddHandler(CommandDd, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open DutyDM settings.",
            });
            commandManager.AddHandler(CommandDdm, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open DutyDM settings.",
            });

            pluginInterface.UiBuilder.Draw += DrawUi;
            pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
            pluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

            InitializeDtrBar(dtrBar);
        }

        private void OnConfigurationSaved(Configuration configuration)
        {
            pushService.Reload(configuration);
            dutyListener.UpdateSubscriptionState();
            partyMonitor.UpdateSubscriptionState();
            UpdateDtrBar();
        }

        /// <summary>Shows an in-game toast reflecting whether the alert reached Discord.</summary>
        public void OnPushResult(PushResult result, string subject)
        {
            try
            {
                if (result.Ok)
                {
                    notificationManager.AddNotification(new Notification
                    {
                        Title = "DutyDM",
                        Content = string.IsNullOrEmpty(subject)
                            ? "Alert sent to your Discord DMs."
                            : $"{subject} - sent to your Discord DMs.",
                        Type = NotificationType.Success,
                    });
                }
                else
                {
                    notificationManager.AddNotification(new Notification
                    {
                        Title = "DutyDM - not sent",
                        Content = $"Couldn't DM you on Discord: {result.Error}",
                        Type = NotificationType.Error,
                    });
                }
            }
            catch (Exception ex)
            {
                pluginLog.Error($"Notification error: {ex.Message}");
            }
        }

        private void InitializeDtrBar(IDtrBar dtrBar)
        {
            try
            {
                dtrEntry = dtrBar.Get("DutyDM");
                dtrEntry.OnClick += _ => configWindow.Toggle();
                UpdateDtrBar();
            }
            catch (Exception ex)
            {
                pluginLog.Error($"DTR bar init failed: {ex.Message}");
            }
        }

        private void UpdateDtrBar()
        {
            if (dtrEntry == null) return;
            bool active = Configuration.Enable || Configuration.PartyFullEnable;
            dtrEntry.Text = active
                ? $"{SeIconChar.Circle.ToIconString()} DutyDM"
                : $"{SeIconChar.Prohibited.ToIconString()} DutyDM";
        }

        private void OnCommand(string command, string args) => configWindow.Toggle();

        private void DrawUi() => WindowSystem.Draw();

        public void ToggleConfigUi() => configWindow.Toggle();

        public void Dispose()
        {
            Configuration.OnSaved -= OnConfigurationSaved;
            dtrEntry?.Remove();
            dutyListener.Dispose();
            partyMonitor.Dispose();
            pushService.Dispose();
            WindowSystem.RemoveAllWindows();
            configWindow.Dispose();

            commandManager.RemoveHandler(CommandMain);
            commandManager.RemoveHandler(CommandDd);
            commandManager.RemoveHandler(CommandDdm);

            pluginInterface.UiBuilder.Draw -= DrawUi;
            pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
            pluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;
        }
    }
}
