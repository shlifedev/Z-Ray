using System.Globalization;
using System.Text;

namespace Lumera.DebuggerKit
{
    // 정의를 사람이 읽는 글자로 쓴다. 힌트 바, 입력 줄의 인자 자리, help 출력, 오류 메시지가 같은 모양을 쓰도록 한 곳에 둔다.
    public static class CheatFormatter
    {
        // 예: player.gold.add <amount:int 1~99999> [reason:string=cheat]
        public static string FormatSignature(CheatDefinition definition)
        {
            StringBuilder builder = new StringBuilder(definition.Id);
            for (int index = 0; index < definition.Parameters.Count; index++)
            {
                builder.Append(' ');
                builder.Append(FormatParameter(definition.Parameters[index], true));
            }

            return builder.ToString();
        }

        // 필수 인자는 <이름:타입>, 생략할 수 있는 인자는 [이름:타입=기본값]으로 쓴다. bDetailed면 숫자 범위도 붙인다.
        public static string FormatParameter(CheatParameter parameter, bool bDetailed)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(parameter.IsRequired ? '<' : '[');
            builder.Append(parameter.Name);
            builder.Append(':');
            builder.Append(parameter.ValueType.Label);
            string range = bDetailed ? FormatRange(parameter) : "";
            if (range.Length > 0)
            {
                builder.Append(' ');
                builder.Append(range);
            }

            string defaultText = parameter.FormatDefault();
            if (defaultText.Length > 0)
            {
                builder.Append('=');
                builder.Append(CommandLine.Quote(defaultText));
            }

            builder.Append(parameter.IsRequired ? '>' : ']');
            return builder.ToString();
        }

        // 범위가 없으면 빈 글자다.
        public static string FormatRange(CheatParameter parameter)
        {
            if (parameter.HasMin && parameter.HasMax)
            {
                return $"{FormatNumber(parameter.Min)}~{FormatNumber(parameter.Max)}";
            }

            if (parameter.HasMin)
            {
                return $"{FormatNumber(parameter.Min)} 이상";
            }

            return parameter.HasMax ? $"{FormatNumber(parameter.Max)} 이하" : "";
        }

        // fromIndex번째부터 남은 인자 자리를 짧게 쓴다(입력 줄의 흐린 안내).
        public static string FormatRemainingParameters(CheatDefinition definition, int fromIndex)
        {
            StringBuilder builder = new StringBuilder();
            for (int index = fromIndex < 0 ? 0 : fromIndex; index < definition.Parameters.Count; index++)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(FormatParameter(definition.Parameters[index], false));
            }

            return builder.ToString();
        }

        private static string FormatNumber(double number)
        {
            return number.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
