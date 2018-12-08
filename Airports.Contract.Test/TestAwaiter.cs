using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Airports.Contract.Test
{

    public class TestAwaitable : IDisposable
    {
        private readonly TestAwaiter _awaiter = new TestAwaiter();
        private System.Timers.Timer killTimer;
        public TestAwaitable(double ms, bool isDebug = false)
        {
            if (!isDebug)
            {
                killTimer = new System.Timers.Timer(ms);
                killTimer.Elapsed += (s, e) => Fail("Time is out");
                killTimer.AutoReset = false;
                killTimer.Start();
            }
        }
        public void Complete()
        {
            _awaiter.Complete();
            Dispose();
            killTimer = null;
        }

        public void Dispose()
        {
            killTimer?.Dispose();
        }

        public void Fail(string message)
        {
            _awaiter.Complete();
            Assert.Fail(message);
        }

        public TestAwaiter GetAwaiter()
        {
            return _awaiter;
        }
        public class TestAwaiter : INotifyCompletion
        {
            private Action _continuation;

            public TestAwaiter()
            {
                IsCompleted = false;
            }

            public bool IsCompleted { get; private set; }

            public void GetResult()
            {
                // Nothing to return.
            }

            public void Complete()
            {
                if (_continuation != null)
                {
                    _continuation();
                    IsCompleted = true;
                }
            }

            public void OnCompleted(Action continuation)
            {
                _continuation += continuation;
            }
        }
    }
}
