using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace StreamCompressor
{
    public class CustomConsoleLoggerOptions : ConsoleFormatterOptions { }

    public class CustomConsoleLogger : ConsoleFormatter
    {
        public const string FormatterName = nameof(CustomConsoleLogger);
        public CustomConsoleLogger() : base(FormatterName) { }
        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider,
            TextWriter textWriter)
        {
            var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            if (message == null)
            {
                return;
            }

            textWriter.WriteLine(message);
        }
    }
}