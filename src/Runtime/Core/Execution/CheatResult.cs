namespace Lumera.DebuggerKit
{
    // 치트 메서드가 돌려주는 결과. 메시지는 콘솔 기록과 메뉴 토스트에 그대로 보인다. 여러 줄이면 줄마다 나뉘어 보인다.
    public readonly struct CheatResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }
        // 콘솔 기록을 비우라는 요청. 내장 명령 clear만 쓴다.
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
