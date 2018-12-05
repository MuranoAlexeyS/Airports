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
        public static Mock<IClient<RequestMoq, ResponseMoq>> Get(int wait, Func<string> resStrategy)
        {
            var mock = new Mock<IClient<RequestMoq, ResponseMoq>>();
            mock.Setup(x => x.AskAsync(It.IsAny<RequestMoq>(), It.IsAny<CancellationToken>()))
            .Returns<RequestMoq, CancellationToken>(async (RequestMoq x, CancellationToken y) =>
            {
                var tcs = new TaskCompletionSource<ResponseMoq>();
                await Task.Delay(wait);
                tcs.SetResult(new ResponseMoq { Data = resStrategy() });
                return await tcs.Task;
            }).Verifiable();
            return mock;

        }
    }
}
