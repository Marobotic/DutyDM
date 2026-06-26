namespace DutyDM
{
    /// <summary>
    /// Where the DutyDM bot lives. Baked into the plugin so players never configure a URL -
    /// the plugin talks straight to the hosted bot on the internet.
    /// </summary>
    internal static class BotEndpoint
    {
        // TEMPORARY: dev machine's port-forwarded home IP, for testing.
        // Swap this to the VPS URL once that's set up. No trailing slash needed.
        public const string BaseUrl = "http://138.68.142.140:1502";

        // Optional shared secret. Must match the bot's PUSH_SECRET env var.
        // Leave empty if the bot runs without a secret.
        public const string Secret = "";
    }
}
