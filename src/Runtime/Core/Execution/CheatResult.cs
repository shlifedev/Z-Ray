namespace Lumera.ZRay
{
    public readonly struct CheatResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }
        internal bool IsClearRequested { get; }

        private CheatResult(bool bSuccess, string message, bool bClearRequested)
        {
            IsSuccess = bSuccess;
            Message = message ?? "";
            IsClearRequested = bClearRequested;
        }

        public static CheatResult Ok(string message = "")
        {
            return new CheatResult(true, message, false);
        }

        public static CheatResult Fail(string message)
        {
            return new CheatResult(false, message, false);
        }

        internal static CheatResult Clear()
        {
            return new CheatResult(true, "", true);
        }
    }
}
