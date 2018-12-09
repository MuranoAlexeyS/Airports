using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Airports.Contract.Test
{

    public class TestAwaitable
    {
        private readonly int _ms;
        private readonly TaskCompletionSource<bool> _tcs;
        private readonly CancellationTokenSource _cts;
        public TestAwaitable(int ms, bool isDebug = false)
        {
            _ms = ms;
            _tcs = new TaskCompletionSource<bool>();
            _cts = new CancellationTokenSource();
        }

        public void Complete()
        {
            _tcs.SetResult(true);
            _cts.Cancel();
        }
        private async Task<bool> Wait(){
            try
            {
                await Task.Delay(_ms, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (_tcs.Task.IsCompleted)
                {
                    return await _tcs.Task;
                }
            }
            return false;
        } 
        public void Fail(string message)
        {
            _tcs.SetResult(false);
            _cts.Cancel();
            Assert.Fail(message);
        }

        public async Task <bool> Awaiter()
        {
            var result = await Task.WhenAny(Wait(), _tcs.Task);
            var isOk = await result;
            if (!isOk) {
                Assert.Fail("Time is out");
            }
            return isOk;
        }
    }
}
