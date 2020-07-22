using System;
using Microsoft.Extensions.Logging;
using IMicrosoftLogger = Microsoft.Extensions.Logging.ILogger;

namespace LoggingStopwatch
{
    public interface IStopwatchLogger
    {
        void Log(string details);
    }

    internal class LambdaLogger : IStopwatchLogger
    {
        private readonly Action<string> loggingFunc;

        public LambdaLogger(Action<string> loggingAction)
        {
            this.loggingFunc = loggingAction;
        }

        public void Log(string details) => loggingFunc(details);
    }

    internal class MicrosoftLoggerWrapper : IStopwatchLogger
    {
        private readonly IMicrosoftLogger microsoftLogger;

        public MicrosoftLoggerWrapper(IMicrosoftLogger microsoftLogger)
        {
            this.microsoftLogger = microsoftLogger;
        }

        public void Log(string details) => microsoftLogger.Log(LogLevel.Information, details);
    }
}
