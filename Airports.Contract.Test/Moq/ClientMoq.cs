using Airports.Contract.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Airports.Contract.Test.Moq
{
    public static class ClientMoq
    {
        /// <summary>
        /// Emulation work and check cancel 10 times 
        /// </summary>
        /// <param name="wait">full time ms for emulate async</param>
        /// <param name="resStrategy">can be string text or throw error</param>
        /// <returns></returns>
        public static Mock<IClient<RequestMoq, ResponseMoq>> Get( Func<string> nameStrategy, Func<string> resStrategy, int wait = 1, bool verifiable = true)
        {
            var mock = new Mock<IClient<RequestMoq, ResponseMoq>>();
            mock.SetupGet(x => x.ClientName).Returns(nameStrategy);
            var rr = mock.Setup(x => x.AskAsync(It.IsAny<RequestMoq>(), It.IsAny<CancellationToken>()))
            .Returns<RequestMoq, CancellationToken>(async (RequestMoq x, CancellationToken y) =>
            {
                var tcs = new TaskCompletionSource<ResponseMoq>();
                for (int i = 0; i < 10; i++)
                {
                    int part = wait / 10;
                    await Task.Delay(part == 0 ? 1 : part).ConfigureAwait(false);

                    if (y.IsCancellationRequested)
                    {
                        tcs.SetCanceled();
                        y.ThrowIfCancellationRequested();
                    }
                }
                tcs.SetResult(new ResponseMoq { Data = resStrategy() });
                return await tcs.Task.ConfigureAwait(false); ;

            });
            if (verifiable)
            {
                rr.Verifiable();
            };
            return mock;

        }
    }
}
