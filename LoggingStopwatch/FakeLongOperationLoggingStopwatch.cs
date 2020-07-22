/* ******************************
   **  Copyright Softwire 2020 ** 
   ****************************** */
using System;

namespace LoggingStopwatch
{
    /// <summary>
    /// Defined in case you ever want something that looks like a ILongOperationLoggingStopwatch but does nothing, and has no overhead.
    /// </summary>
    public class FakeLongOperationLoggingStopwatch : FakeDisposable, ILongOperationLoggingStopwatch
    {
        public IDisposable TimeInnerOperation() => new FakeDisposable();
    }

    public class FakeDisposable : IDisposable
    {
        public virtual void Dispose() { /*Do Nothing*/ }
    }
}
