using Airports.Contract.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Airports.Contract
{
    public abstract class ClientRollup<T, V> : IClientBalancer<T, V>
    {
        private readonly IReadOnlyList<IClient<T, V>> _clients;
        private int _counter = 0;
        private int len = 0;
        public ClientRollup(IEnumerable<IClient<T, V>> clients)
        {
            _clients = clients.Where(x => x != null).ToArray();
            if (_clients.Count == 0)
            {
                throw new ArgumentException("No Clients", nameof(clients));
            }
            len = _clients.Count;
        }
        public IClient<T, V> GetNext()
        {
            int d = Interlocked.Increment(ref _counter);
            return _clients[Math.Abs(d % len)];
        }
    }
}
