using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Airports.Contract.Interfaces
{
    public interface IHttpJsonClient
    {
       Task<T> GetAsync<T>(CancellationToken token, Uri uri);
    }
}
