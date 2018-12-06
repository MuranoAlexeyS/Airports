using Airports.Contract.Interfaces;
using Airports.Contract.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Airports.Contract.Test.Moq
{
    public class TestedCoordinator : Coordinator<RequestMoq, ResponseMoq>
    {
        public TestedCoordinator(IClientBalancer<RequestMoq, ResponseMoq> clients) : base(clients)
        {
        }
    }
}
