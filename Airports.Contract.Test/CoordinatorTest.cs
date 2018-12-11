using Airports.Contract.Interfaces;
using Airports.Contract.Test.Moq;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Airports.Contract.Test
{
    class CoordinatorTest
    {
        const int requestCount = 1 << 16;
        private int _simpleTestReadCounter = 0;
        [Test]
        [TestCase(requestCount)]
        public async Task SimpleTest(int count)
        {
            var waiter = new TestAwaitable(count);
            var moqClients = Enumerable.Range(0, 4).Select(x => ClientMoq.Get(() => $"SimpleTest{x}", () => x.ToString())).ToList();
            var rollup = new TestRollup(moqClients.Select(x => x.Object));
            var coord = new TestedCoordinator(rollup, (x, y, e) => waiter.Fail($"Client Error Sended {x.GetIdentifier()}\n ex: {e}"), (x) => waiter.Fail($"Canceled {x.GetIdentifier()}"),
                onComplete: (x, y) =>
                {
                    if (Interlocked.Increment(ref _simpleTestReadCounter) == count)
                    {
                        waiter.Complete();
                    }
                });
            await SuccessResults(count, waiter, coord, x => x.ToString());
            moqClients.ForEach(x => x.Verify(y => y.AskAsync(It.IsAny<RequestMoq>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce));
            await Task.CompletedTask;
        }

        [Test]
        [TestCase(300)]
        public async Task CancelTest(int wait)
        {
            var waiter = new TestAwaitable(wait);
            var clients = new[] { ClientMoq.Get(() => "", () => "wait", wait) };
            var rollupSingleWaiting = new TestRollup(clients.Select(x => x.Object));
            var coord = new TestedCoordinator(rollupSingleWaiting, (x, y, e) => waiter.Fail($"Client Error Sended {x.GetIdentifier()}\n ex: {e}"), (x) => waiter.Complete());
            const string name = "cancel";
            string id = await coord.GetToken(new RequestMoq(name));
            await coord.Cancel(id);
            Models.Response<ResponseMoq> r = new Models.Response<ResponseMoq>();
            var t = coord.Result(id);
            r = await t;
            await waiter.Awaiter();

            Assert.IsTrue(r.GetState() == Models.ResponseState.Deleted);
            await Task.CompletedTask;
        }

        private int _repeatTestReadCounter = 0;
        [Test]
        [TestCase(1 << 12)]
        public async Task RepeatTest(int count)
        {
            var waiter = new TestAwaitable(count);
            var clients = new[] { ClientMoq.Get(() => "wait", () => "wait", 100) };
            var rollupSingleWaiting = new TestRollup(clients.Select(x => x.Object));
            var coord = new TestedCoordinator(rollupSingleWaiting, (x, c, e) => Assert.Fail(), (x) => Assert.Fail(), onComplete: (x, y) =>
            {
                if (Interlocked.Increment(ref _repeatTestReadCounter) == 2)
                {
                    waiter.Complete();
                }
            });
            await SuccessResults(count, waiter, coord, x => (x % 2).ToString());
            foreach (var x in clients)
            {
                x.Verify(y => y.AskAsync(It.IsAny<RequestMoq>(), It.IsAny<CancellationToken>()), Times.AtMost(2));
                x.Verify(y => y.AskAsync(It.IsAny<RequestMoq>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            }
            await Task.CompletedTask;

        }

        [Test]
        [TestCase(1 << 12)]
        public async Task ErrorTest(int count)
        {
            int failMod = 7;
            int cancelMod = 13;
            int requestMod = 23;
            // var waiter = new TestAwaitable(1);
            var clients = Enumerable.Range(0, 35).Select(x => ClientMoq.Get(() => x % failMod == 0 ? "fail" : "good", () =>
         {
             if (x % failMod == 0)
             {
                 throw new SuccessException(x.ToString());
             }
             if (x % cancelMod == 0)
             {
                 throw new TaskCanceledException(x.ToString());
             }
             return x.ToString();
         }).Object);
            var error = new ConcurrentBag<string>();
            var cancel = new ConcurrentBag<string>();
            var rollup = new TestRollup(clients);
            var coord = new TestedCoordinator(rollup, (r, client, ex) =>
            {
                error.Add(r.GetIdentifier());
                if (client == "fail")
                {
                    if (!(ex is SuccessException))
                    {
                        Assert.Fail($"Incorrect Exception: {ex}");
                    }
                }
                else
                {
                    Assert.Fail($"Exception into correct client: {client}");
                }
            }, (r) =>
            {
                cancel.Add(r.GetIdentifier());
            });
            var list = await Task.WhenAll(Enumerable.Range(0, count).AsParallel().Select(async (x) =>
            {
                return await
                await coord.GetToken(new RequestMoq((x % requestMod).ToString())).ContinueWith(async (req) =>
                 {
                     var y = await req;
                     var t = coord.Result(y);
                     var r = await t;
                     return new { request = y, response = r };

                 });
            }).ToList());

            var unionList = list.Where(x => x.response.GetState() != Models.ResponseState.Processed).ToArray();
            while (list.Any()) //wait all done
            {
                var part = await Task.WhenAll(list.AsParallel().Select(async x =>
                {
                    var t = coord.Result(x.request);
                    var r = await t;
                    return new { request = x.request, response = r };
                }).ToList());
                unionList = unionList.Union(part.Where(x => x.response.GetState() != Models.ResponseState.Processed)).ToArray();
                list = part.Where(x => x.response.GetState() == Models.ResponseState.Processed).ToArray();
            }
            var requestWithErrors = error.Distinct().ToArray();
            var requestWithCancel = cancel.Distinct().ToArray();
            foreach (var group in unionList.Where(x => x != null).GroupBy(x => x.request, y => y.response.GetState()))
            {
                if (group.Count() > 1 && group.All(x => x == Models.ResponseState.Deleted))
                {
                    Assert.Fail($"All request {group.Key} can be Fails");
                }
                if (group.Any(x => x == Models.ResponseState.Deleted))
                {
                    if (!(requestWithErrors.Contains(group.Key.ToString()) || requestWithCancel.Contains(group.Key.ToString())))
                    {
                        Assert.Fail($"Invaild Error for {group.Key}");
                    }
                }
            }

            await Task.CompletedTask;

        }

        private int _lifeTestCounter;
        [Test]
        [TestCase(1 << 8)]
        public async Task LifeTimeTest(int count)
        {
            int failMod = 7;
            int cancelMod = 13;

            var clients = Enumerable.Range(0, 35).Select(x => ClientMoq.Get(() => x % failMod == 0 ? "fail" : "good", () =>
            {
                if (x % failMod == 0)
                {
                    throw new SuccessException(x.ToString());
                }
                if (x % cancelMod == 0)
                {
                    throw new TaskCanceledException(x.ToString());
                }
                return x.ToString();
            }).Object);
            var rollup = new TestRollup(clients);
            var waiter = new TestAwaitable(5000);
            var coord = new TestedCoordinator(rollup,
                onRemove: (x) =>
                {
                    if (Interlocked.Increment(ref _lifeTestCounter) == count)
                    {
                        waiter.Complete();
                    }
                }, msLife: 1);
            var list = await Task.WhenAll(Enumerable.Range(0, count).AsParallel()
                .Select((x) => coord.GetToken(new RequestMoq(x.ToString()))).ToList());
            await waiter.Awaiter();
            Assert.IsTrue(_lifeTestCounter == count);
            await Task.CompletedTask;
        }
        private static async Task SuccessResults(int count, TestAwaitable waiter, TestedCoordinator coord, Func<int, string> requestFunc)
        {
            var list = Task.WhenAll(Enumerable.Range(0, count).AsParallel()
                .Select(async (x) => await coord.GetToken(new RequestMoq(requestFunc(x))))
                .Select(async (req) =>
                {
                    var y = await req;
                    var t = coord.ResultAsync(y);
                    var r = await t;
                    return new { request = y, response = r };
                }));
            await waiter.Awaiter();
            var result = await list;
            Assert.IsTrue(result.All(x => x?.response != null));
        }
    }
}
