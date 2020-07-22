/* ******************************
   **  Copyright Softwire 2020 ** 
   ****************************** */
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace LoggingStopwatch
{
    public interface ILongOperationLoggingStopwatch : IDisposable
    {
        IDisposable TimeInnerOperation();
    }

    /// <summary>
    /// Similar to <see cref="LoggingStopwatch"/>, but designed for use in situations
    /// where you want to know how long is spent in a particular block of code, over
    /// the course of multiple iterations, including when those iterations are run in parallel.
    /// 
    /// Use a single instance of the Stopwatch around the whole execution, and then
    /// repeated instances of TimeInnerOperation around the section(s?) that you want to
    /// be included in the timing.
    ///
    /// As each inner block is disposed, you'll (optionally) get a log of the number of reps
    /// completed, the progress towards completion and a prediction of completion time)
    ///
    /// When the outerStopwatch is disposed, it will log a summary of the overall time
    /// taken, and (optionally) the number of distinct threads, and a breakdown of the
    /// time spent on each.
    ///
    /// Usage:
    /// <code>
    ///     using(var stopwatch = new LongOperationLoggingStopwatch("SomeMethod", logger))
    ///     {
    ///         foreach(var thing in myThings)
    ///         {
    ///            BoringSetup();
    ///            using(stopwatch.TimeInnerOperation())
    ///            {
    ///                InterestingMethod(thing);
    ///            }
    ///            MoreBoringWork()
    ///         }
    ///     }//Log will be written here.
    /// </code>
    /// 
    /// Also accepts anonymous lambda as a logger: <c>new LongOperationLoggingStopwatch("AnotherMethod", (text) => myLogger.WriteLog(text))</c>
    /// </summary>
    public class LongOperationLoggingStopwatch : LoggingStopwatch, ILongOperationLoggingStopwatch
    {
        private readonly LongLoggingSettings settings;
        private int iterationsCompleted = 0;
        private int activeExecutions = 0;
        private readonly ConcurrentDictionary<int, TimeSpan> innerTimings = new ConcurrentDictionary<int, TimeSpan>();
        private const int TotalInnerOperationRecord = -1; //ManagedThreadId is guaranteed to be positive.
        private readonly InnerOperationExecutionTimer innerTimingHandler;

        #region Constructors
        /// <inheritdoc cref="LongOperationLoggingStopwatch"/>
        /// <param name="identifier">
        /// String to identify in the logs what operation was timed.
        /// A unique identifier will be generated in addition to this, so that multiple executions are distinguishable.
        /// </param>
        /// <param name="logger">
        /// Logger object to perform the logging when complete
        /// </param>
        /// <param name="loggingSettings">
        /// Override the defaults for how the inner operations get logged.
        /// </param>
        public LongOperationLoggingStopwatch(string identifier, IStopwatchLogger logger, LongLoggingSettings loggingSettings = null) :
            base(identifier, logger)
        {
            settings?.Validate();
            settings = loggingSettings ?? new LongLoggingSettings();
            LogInitiationMessage();
            innerTimingHandler = new InnerOperationExecutionTimer(this);
            base.timer.Start(); // Zero out the time taken in this ctor, since the end of the base ctor.
        }

        private void LogInitiationMessage()
        {
            var initiationMessage = "Started.";
            var reps = settings.ExpectedNumberOfIterations;
            if (reps.HasValue)
            {
                initiationMessage += $"|Expecting to complete {reps} iterations of the inner operation.";
            }

            Log(initiationMessage);
        }

        /// <inheritdoc/>
        /// <param name="identifier">Defers to Inherited paramDoc</param>
        /// <param name="loggingAction">Method to call whenever some text should be logged.</param>
        /// <param name="settings">Defers to Inherited paramDoc</param>
        public LongOperationLoggingStopwatch(string identifier, Action<string> loggingAction, LongLoggingSettings settings = null)
            : this(identifier, new LambdaLogger(loggingAction), settings)
        {
        }
        #endregion
        
        public IDisposable TimeInnerOperation()
        {
            Interlocked.Increment(ref activeExecutions);
            innerTimingHandler.StartOperation();
            return innerTimingHandler;
        }

        // Needs to be fully thread-safe!
        private void RecordInnerExecutionComplete(TimeSpan elapsedTime, int threadId)
        {
            try
            {
                var newCompletedCount = Interlocked.Increment(ref iterationsCompleted);
                var totalOuterElapsedTime_MS = base.timer.ElapsedMilliseconds;

                innerTimings.AddOrUpdate(
                    TotalInnerOperationRecord,
                    elapsedTime,
                    (_, previousElapsedTotalTime) => previousElapsedTotalTime + elapsedTime);

                if (settings.ReportPerThreadTime)
                {
                    innerTimings.AddOrUpdate(
                        threadId,
                        elapsedTime,
                        (_, previousElapsedPerThreadTime) => previousElapsedPerThreadTime + elapsedTime);
                }

                var message = DeterminePerExecutionLoggingMessageIfAny(newCompletedCount, totalOuterElapsedTime_MS, settings);
                if (message != null)
                {
                    Log(message);
                }
            }
            catch (Exception e)
            {
                // We really don't expect exceptions above, but if anything goes
                // wrong we don't want it to bring down the calling operation.
                Log("Swallowing exception in LoggingStopwatch: " + e.ToString());
            }
            finally
            {
                Interlocked.Decrement(ref activeExecutions);
            }
        }

        // This is deliberately static, so that we're forced to explicitly pass in captured
        // values and can't use instance fields that might have been updated by other threads.
        private static string DeterminePerExecutionLoggingMessageIfAny(int newCompletedCount, long totalOuterElapsedTime_MS, LongLoggingSettings settings)
        {
            if (newCompletedCount % settings.InnerOperationLoggingFrequency == 0)
            {
                var logMessage = $"Progress: ({newCompletedCount}) operations completed.";

                var completionPercentage = Decimal.Divide(newCompletedCount, settings.ExpectedNumberOfIterations ?? 1);

                if (settings.ReportPercentageCompletion)
                {
                    logMessage += $"|{completionPercentage:0.00%}";
                }

                if (settings.ReportProjectedCompletionTime)
                {
                    var remainingPercentage = 1 - completionPercentage;
                    var remainingMultiplier = remainingPercentage / completionPercentage;

                    var projectedTotalTimeRemaining_MS = totalOuterElapsedTime_MS * remainingMultiplier;
                    var projectedOuterCompletionTime = DateTime.UtcNow.AddMilliseconds((double) projectedTotalTimeRemaining_MS);
                    logMessage += $"|Projected completion time: {projectedOuterCompletionTime}Z (UTC)";
                }

                return logMessage;
            }

            return null;
        }

        public override void Dispose()
        {
            var overallTime = base.timer.Elapsed;
            //TODO: What about errors?

            if (activeExecutions > 0)
            {
                Log("WARNING: Some inner executions were still outstanding when the outer stopwatch was Disposed! Reporting will ignore those executions.");
            }

            if (iterationsCompleted == 0)
            {
                Log($"Completed in {overallTime}");
                return;
            }

            var threadCountMessage = $"Inner operations were spread over {innerTimings.Keys.Count} thread(s):";

            //Log the high-level results.
            var innerTotalTimeSpan = innerTimings[TotalInnerOperationRecord];
            var primaryLogMessage = $"Completed|{iterationsCompleted} Inner operations ran for a linear total of: {innerTotalTimeSpan}|The outer scope ran for an elapsed time of: {overallTime}";
            if (settings.ReportThreadCount && !settings.ReportPerThreadTime)
            {
                primaryLogMessage += $"|{threadCountMessage}";
            }
            
            Log(primaryLogMessage);
            
            //Log per-thread results if requested.
            if (settings.ReportPerThreadTime)
            {
                var innerThreadTimes = innerTimings.Where(kvp => kvp.Key != TotalInnerOperationRecord).Select(kvp => kvp.Value).ToArray();

                if (innerThreadTimes.Length == 1)
                {
                    Log("All inner operations ran on a single thread.");
                }
                else
                {
                    Log(threadCountMessage);
                    for (int i = 0; i < innerThreadTimes.Length; i++)
                    {
                        var threadTimeSpan = innerThreadTimes[i];
                        Log($"| - Time spent on thread #{i}: {threadTimeSpan}");
                    }
                }
            }
        }

        /// <summary>
        /// We'll make a new copy of this class for every loop, including
        /// separate copies for loops running on separate threads.
        /// </summary>
        private class InnerOperationExecutionTimer : IDisposable
        {
            private readonly LongOperationLoggingStopwatch parent;
            private readonly ThreadLocal<Stopwatch> timer = new ThreadLocal<Stopwatch>(() => new Stopwatch(), false);
            
            public InnerOperationExecutionTimer(LongOperationLoggingStopwatch parent)
            {
                this.parent = parent;
            }

            public void StartOperation()
            {
                timer.Value.Restart();
            }

            public void Dispose()
            {
                var elapsed = timer.Value.Elapsed;
                var threadId = Thread.CurrentThread.ManagedThreadId;
                parent.RecordInnerExecutionComplete(elapsed, threadId);
            }

        }
    }
}
