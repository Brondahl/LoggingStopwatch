using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LoggingStopwatch;
using NUnit.Framework;

namespace Tests
{
    public class Tests
    {
        private const int TestReps = 100_000_000;

        private LongOperationLoggingStopwatch CreateStopwatch() =>
            new LongOperationLoggingStopwatch(
                "test",
                Console.WriteLine,
                new LongLoggingSettings
                {
                    InnerOperationLoggingFrequency = TestReps / 2,
                    ReportPerThreadTime = true
                });

        [Test]
        public void SingleThreaded_Baseline_Increment()
        {
            var total = 0;
            using (var timer = CreateStopwatch())
            {
                for (int i = 0; i < TestReps; i++)
                {
                    total++;
                }
            }
            Assert.That(total == TestReps);
        }

        [Test]
        public void SingleThreaded_Baseline_InterlockedIncrement()
        {
            var total = 0;
            using (var timer = CreateStopwatch())
            {
                for (int i = 0; i < TestReps; i++)
                {
                    Interlocked.Increment(ref total);
                }
            }
            Assert.That(total == TestReps);
        }


        [Test]
        public void SingleThreaded_Baseline_DateTime_UtcNow()
        {
            var total = 0;
            using (var timer = CreateStopwatch())
            {
                for (int i = 0; i < TestReps; i++)
                {
                    var y = DateTime.UtcNow;
                }
            }
            Assert.That(total == 0);
        }

        [Test]
        public void SingleThreaded_Baseline_DateTime_Now()
        {
            var total = 0;
            using (var timer = CreateStopwatch())
            {
                for (int i = 0; i < TestReps; i++)
                {
                    var y = DateTime.Now;
                }
            }
            Assert.That(total == 0);
        }

        [Test]
        public void SingleThreaded_OverheadTest()
        {
            var total = 0;
            using (var timer = CreateStopwatch())
            {
                for (int i = 0; i < TestReps; i++)
                {
                    using (timer.TimeInnerOperation())
                    {
                        total++;
                    }
                }
            }
            Assert.That(total == TestReps);
        }

        [Test]
        public void SingleThreaded_LowVolume_Baseline_Increment()
        {
            var total = 0;
            using (var timer = new LongOperationLoggingStopwatch("test", _ => { }, new LongLoggingSettings { InnerOperationLoggingFrequency = 1 }))
            {
                for (int i = 0; i < 1_000_000; i++)
                {
                    total++;
                }
            }
            Assert.That(total == 1_000_000);
        }

        [Test]
        public void SingleThreaded_LowVolume_OverheadTest_WithLogging()
        {
            var total = 0;
            using (var timer = new LongOperationLoggingStopwatch("test", _ => { }, new LongLoggingSettings { InnerOperationLoggingFrequency = 1 }))
            {
                for (int i = 0; i < 1_000_000; i++)
                {
                    using (timer.TimeInnerOperation())
                    {
                        total++;
                    }
                }
            }
            Assert.That(total == 1_000_000);
        }

        [Test]
        public void MultiThreaded_BaseLine_InterlockedIncrement()
        {
            var total = 0;
            using (var timer = CreateStopwatch())
            {
                var reps = Enumerable.Repeat(1, TestReps);
                Parallel.ForEach(reps, new ParallelOptions{MaxDegreeOfParallelism = 2},  _ =>
                {
                    Interlocked.Increment(ref total);
                });
            }
            Assert.That(total == TestReps);
        }

        [Test]
        public void MultiThreaded_OverheadTest()
        {
            var total = 0;
            using (var timer = CreateStopwatch())
            {
                var reps = Enumerable.Repeat(1, TestReps);
                Parallel.ForEach(reps, new ParallelOptions { MaxDegreeOfParallelism = 2 }, _ =>
                {
                    using (timer.TimeInnerOperation())
                    {
                        Interlocked.Increment(ref total);
                    }
                });
            }
            Assert.That(total == TestReps);
        }
    }
}