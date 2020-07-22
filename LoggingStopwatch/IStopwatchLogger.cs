using System;

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
}
