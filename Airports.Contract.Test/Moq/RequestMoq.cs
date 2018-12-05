using Airports.Contract.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Airports.Contract.Test.Moq
{
    public class RequestMoq : IRequest
    {
        private readonly string _name;

        public RequestMoq(string name)
        {
            this._name = name;
        }
        public string GetIdentifier()
        {
            return _name;
        }
    }
}
