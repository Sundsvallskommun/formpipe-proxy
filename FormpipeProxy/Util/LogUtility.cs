using System;
using System.Text.RegularExpressions;

namespace FormpipeProxy.Util
{
    public class LogUtility
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        public static string SanitizeForLogging(string input) =>
            input is null ? null
                : Regex.Replace(
                    Regex.Replace(
                        Regex.Replace(input, @"[\r\n]", " ", RegexOptions.None, RegexTimeout),
                        @"[^\x20-\x7E]", "", RegexOptions.None, RegexTimeout),
                    @"[%\\]", "", RegexOptions.None, RegexTimeout);
    }
}
