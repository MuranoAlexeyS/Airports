using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Airports.Contract.Interfaces
{
    public interface ICoordinator<T,V>
    {
        Task<string> GetToken(T request);
        Task<V> Result(string token);
        void Cancel(string token);
    }
}
