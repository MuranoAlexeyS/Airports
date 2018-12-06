using Airports.Contract.Interfaces;
using Airports.Contract.Test.Moq;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Airports.Contract.Test
{
    class CoordinatorTest
    {
        private List<Mock<IClient<RequestMoq, ResponseMoq>>> _moqClients;
        private TestRollup _rollup;
        private TestRollup _rollupSingleWaiting;
        private TestRollup _rollupError;
        private const int _clientCount = 4;
        private const int powtwo = 12;
        private const int waittime = 300;

        [SetUp]
        public void Setup()
        {
            _moqClients = Enumerable.Range(0, _clientCount).Select(x => ClientMoq.Get(1, () => x.ToString())).ToList();
            _rollup = new TestRollup(_moqClients.Select(x => x.Object));
            _rollupSingleWaiting = new TestRollup(new[] { ClientMoq.Get(waittime, () => "wait", false).Object });
            _rollupError = new TestRollup(Enumerable.Range(0, _clientCount).Select(x => ClientMoq.Get(1, () => x % 2 > 0 ? x.ToString() : throw new Exception(x.ToString()), false)).ToList().Select(x => x.Object));

        }

        [Test]
        public async Task SimpleTest()
        {
            var coord = new TestedCoordinator(_rollup);
            var token = new CancellationTokenSource().Token;
            var list = await Task.WhenAll(Enumerable.Range(0, (1 << powtwo)).AsParallel().Select((x) => coord.GetToken(new RequestMoq(x.ToString()))).ToList());
            while (list.Length > 0)
            {
                int len = list.Length;
                await Task.Delay(1); //give chance
                list = (await Task.WhenAll(list.AsParallel().Select(async x =>
                  {
                      var t = coord.Result(x);
                      var r = await t;
                      if (r.GetState() == Models.ResponseState.Readed)
                      {
                          Assert.IsTrue(r.GetData() != null);
                          Assert.IsTrue(!string.IsNullOrEmpty(r.GetData().Data));
                      }
                      return new { req = x, res = r };
                  }).ToList())).Where(x => x.res.GetState() != Models.ResponseState.Readed).Select(x => x.req).ToArray();
                _moqClients.ForEach(x => x.Verify(y => y.AskAsync(It.IsAny<RequestMoq>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce));
                Assert.LessOrEqual(list.Length, len);
            }
            await Task.CompletedTask;
        }
        [Test]
        public async Task CancelTest()
        {
            var coord = new TestedCoordinator(_rollupSingleWaiting);
            var token = new CancellationTokenSource().Token;
            const string name = "cancel";
            string id = await coord.GetToken(new RequestMoq(name));
            await coord.Cancel(id);
            var t = coord.Result(id);
            var r = await t;
            Assert.IsTrue(r.GetState() == Models.ResponseState.Deleted);
        }
    }
}
