using LoggingStopwatch;
using NUnit.Framework;

namespace Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            var total = 0;
            using (var timer = new LongOperationLoggingStopwatch("test", _ => { }))
            {
                for (int i = 0; i < 50_000_000; i++)
                {
                    using (timer.TimeInnerOperation())
                    {
                        total++;
                    }
                }
                
            }
        }
    }
}