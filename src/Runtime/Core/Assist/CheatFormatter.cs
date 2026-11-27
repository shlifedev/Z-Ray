using System.Globalization;
using System.Text;

namespace Lumera.ZRay
{
    public static class CheatFormatter
    {
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

        private static string FormatNumber(double number)
        {
            return number.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
