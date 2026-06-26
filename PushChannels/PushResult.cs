namespace DutyDM.PushChannels
{
    /// <summary>Outcome of an attempt to push a duty-pop alert through the bot.</summary>
    public readonly struct PushResult
    {
        public bool Ok { get; }

        /// <summary>Human-readable failure reason (null on success).</summary>
        public string? Error { get; }

        private PushResult(bool ok, string? error)
        {
            Ok = ok;
            Error = error;
        }

        public static PushResult Success => new(true, null);

        public static PushResult Fail(string error) => new(false, error);
    }
}
