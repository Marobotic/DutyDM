using System;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using DutyDM.PushChannels;

namespace DutyDM.Services
{
    /// <summary>Builds the active push channel from config and sends notifications through it.</summary>
    public class PushService : IDisposable
    {
        private IPushChannel channel = null!;
        private readonly IPluginLog pluginLog;
        private bool disposed;

        public PushService(Configuration configuration, IPluginLog pluginLog)
        {
            this.pluginLog = pluginLog;
            Reload(configuration);
        }

        public void Reload(Configuration configuration)
        {
            channel = new DiscordBotChannel(configuration.DiscordUsername);
        }

        public async Task<PushResult> SendNotificationAsync(string title, string message)
        {
            try
            {
                return await channel.SendAsync(title, message, pluginLog).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                pluginLog.Error($"Push failed: {ex.GetType().Name} - {ex.Message}");
                return PushResult.Fail(ex.Message);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
