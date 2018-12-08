using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Airports.Contract.Interfaces
{
    public interface IClient<T, V>
    {
        Task<V> AskAsync(T request, CancellationToken cancellationToken);
        string ClientName { get; }
    }
}
