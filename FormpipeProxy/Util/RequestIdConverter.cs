using System;
using System.IO;
using System.Threading;

using log4net.Core;
using log4net.Layout.Pattern;

namespace FormpipeProxy.Util
{
    public class RequestIdConverter : PatternLayoutConverter
    {
        private static readonly AsyncLocal<string> RequestId = new AsyncLocal<string>();

        protected override void Convert(TextWriter writer, LoggingEvent loggingEvent)
        {
            try
            {
                if (RequestId.Value == default)
                {
                    RequestId.Value = Guid.NewGuid().ToString("D");
                }
                writer.Write(RequestId.Value);
            }
            catch
            {
                writer.Write(Guid.Empty.ToString("D"));
            }
        }
    }
}