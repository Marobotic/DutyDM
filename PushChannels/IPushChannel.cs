using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace DutyDM.PushChannels
{
    /// <summary>A target that a notification can be sent through.</summary>
    public interface IPushChannel
    {
        Task<PushResult> SendAsync(string title, string message, IPluginLog pluginLog);
    }
}
