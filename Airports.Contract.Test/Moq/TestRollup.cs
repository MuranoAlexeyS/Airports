using System;
using System.Collections.Generic;
using System.Text;
using Airports.Contract.Interfaces;

namespace Airports.Contract.Test.Moq
{
    public class TestRollup : ClientRollup<RequestMoq, ResponseMoq>
    {
        public TestRollup(IEnumerable<IClient<RequestMoq, ResponseMoq>> clients) : base(clients)
        {
        }
    }
}
