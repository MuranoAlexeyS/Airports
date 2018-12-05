using Airports.Contract;
using Airports.Contract.Interfaces;
using Airports.Contract.Test.Moq;
using Moq;
using NUnit.Framework;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    public class Tests
    {
        private Mock<IClient<RequestMoq, ResponseMoq>>[] _moqClients;
        private TestRollup _rollup;
        private const int _clientCount = 4;
        private const int powtwo = 16;

        [SetUp]
        public void Setup()
        {
            _moqClients = Enumerable.Range(0, _clientCount).Select(x => ClientMoq.Get(1, () => x.ToString())).ToArray();
            _rollup = new TestRollup(_moqClients.Select(x => x.Object));
        }

        [Test]
        public void Test1()
        {

            var token = new CancellationTokenSource().Token;
            Parallel.For(0, ((1 << powtwo)), new ParallelOptions() { MaxDegreeOfParallelism = _clientCount }, async (x) => await _rollup.GetNext().AskAsync(new RequestMoq(x.ToString()), token));
            foreach (var c in _moqClients)
            {
                c.Verify(x => x.AskAsync(It.IsAny<RequestMoq>(), It.IsAny<CancellationToken>()), Times.Between((int)((1 << powtwo) / _clientCount - powtwo), (int)(1 << powtwo) / _clientCount + powtwo, Range.Inclusive));
            }
        }
    }
}