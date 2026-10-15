namespace DutyDM
{
    /// <summary>
    /// Where the DutyDM bot lives. Baked into the plugin so players never configure a URL -
    /// the plugin talks straight to the hosted bot on the internet.
    /// </summary>
    internal static class BotEndpoint
    {
        // The bot (bot/ in this repo) on the marobotic.dev droplet, behind nginx. No trailing slash needed.
        public const string BaseUrl = "https://marobotic.dev/dutydm";

        // Optional shared secret. Must match the bot's PUSH_SECRET env var.
        // Leave empty if the bot runs without a secret.
        public const string Secret = "";
    }
}
