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
        private int _resetFlag = 0;
        private const int _resetPoint = 1 << 30;
        public ClientRollup(IEnumerable<IClient<T,V>> clients)
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
            if (d > _resetPoint)
            {
                if (Interlocked.Exchange(ref _resetFlag, 1) == 0)
                {
                    Interlocked.Exchange(ref _counter, 0);
                }
            }
            else {
                Interlocked.Exchange(ref _resetFlag, 0);
            }
            return _clients[d % len];
        }
    }
}
