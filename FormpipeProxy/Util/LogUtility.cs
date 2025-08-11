using System.Text.RegularExpressions;

namespace FormpipeProxy.Util
{
    public class LogUtility
    {
        public static string SanitizeForLogging(string input) =>
            input is null ? null
                : Regex.Replace(
                    Regex.Replace(
                        Regex.Replace(input, "[\\r\\n]", " "), 
                        "[^\\x20-\\x7E]", ""), 
                    "[%\\\\]", "");

    }
}
