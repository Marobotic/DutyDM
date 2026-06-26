using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DutyDM.PushChannels
{
    /// <summary>
    /// Sends a notification to the hosted DutyDM Discord bot, which DMs the user.
    /// POSTs { username, title, message, secret } to {BotEndpoint.BaseUrl}/notify.
    /// </summary>
    public class DiscordBotChannel : IPushChannel
    {
        // One shared client avoids socket exhaustion across reloads.
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

        private readonly string username;

        public DiscordBotChannel(string username)
        {
            this.username = (username ?? "").Trim();
        }

        public async Task<PushResult> SendAsync(string title, string message, IPluginLog pluginLog)
        {
            if (string.IsNullOrWhiteSpace(username))
                return PushResult.Fail("Enter your Discord username first.");

            var url = $"{BotEndpoint.BaseUrl.TrimEnd('/')}/notify";
            var payload = JsonConvert.SerializeObject(new
            {
                username,
                title = title ?? string.Empty,
                message = message ?? string.Empty,
                secret = BotEndpoint.Secret,
            });

            try
            {
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var response = await Http.PostAsync(url, content).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return PushResult.Success;

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var reason = ExtractError(body);
                pluginLog.Warning($"DutyDM bot returned {(int)response.StatusCode}: {reason}");
                return PushResult.Fail(MapError((int)response.StatusCode, reason));
            }
            catch (TaskCanceledException)
            {
                return PushResult.Fail("Timed out reaching the bot.");
            }
            catch (HttpRequestException ex)
            {
                return PushResult.Fail($"Couldn't reach the DutyDM bot. ({ex.Message})");
            }
            catch (Exception ex)
            {
                pluginLog.Error($"DutyDM push error: {ex}");
                return PushResult.Fail(ex.Message);
            }
        }

        private static string ExtractError(string body)
        {
            try { return JObject.Parse(body)["error"]?.ToString() ?? ""; }
            catch { return ""; }
        }

        private static string MapError(int status, string reason) => (status, reason) switch
        {
            (401, _) => "The plugin and bot secret don't match. Try updating the plugin.",
            (_, "user_not_found") => "Your username wasn't found in the server. Did you join, and is it spelled exactly right?",
            (_, "dm_failed") => "I couldn't DM you. Enable 'Allow direct messages from server members' in Discord privacy settings.",
            (429, _) => "Slow down - try again in a few seconds.",
            (503, _) => "The bot is still starting up. Try again shortly.",
            (400, "missing_username") => "Enter your Discord username first.",
            _ => $"Bot returned status {status}{(string.IsNullOrEmpty(reason) ? "" : $" ({reason})")}.",
        };
    }
}
