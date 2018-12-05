using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Airports.Contract.Interfaces
{
    public interface IClientBalancer<T, V>
    {
        IClient<T,V> GetNext();
    }
}
