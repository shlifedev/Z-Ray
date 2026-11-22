using System;

namespace Lumera.DebuggerKit
{
    // 치트 안에서 "할 수 없다"를 알리는 예외. 메시지만 실패로 보이고, 버그로 취급하지 않는다(호스트가 예외 로그를 남기지 않는다).
    // CheatResult.Fail을 돌려줄 수 없는 곳(값 치트의 setter, 깊은 호출)에서 쓴다.
    public sealed class CheatException : Exception
    {
        public CheatException(string message) : base(message)
        {
        }
    }
}
