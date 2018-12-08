using Airports.Contract.Interfaces;
using Airports.Contract.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Airports.Contract.Test.Moq
{
    public class TestedCoordinator : Coordinator<RequestMoq, ResponseMoq>
    {
        private readonly Action<RequestMoq, string, Exception> _onError;
        private readonly Action<RequestMoq> _onCancel;
        private readonly Action<RequestMoq> _onAdd;
        private readonly Action<RequestMoq, ResponseMoq> _onComplete;
        private readonly Action<string> _onRemove;
        private readonly Action<string> _onRead;

        public TestedCoordinator(IClientBalancer<RequestMoq, ResponseMoq> clients,
            Action<RequestMoq, string, Exception> onError = null,
            Action<RequestMoq> onCancel = null,
            Action<RequestMoq> onAdd = null,
            Action<RequestMoq, ResponseMoq> onComplete = null,
            Action<string> onRemove = null,
            Action<string> onRead = null) : base(clients)
        {
            this._onError = onError;
            this._onCancel = onCancel;
            this._onAdd = onAdd;
            this._onComplete = onComplete;
            this._onRemove = onRemove;
            this._onRead = onRead;
        }

        public override void OnAdd(RequestMoq id)
        {
            _onAdd?.Invoke(id);
        }

        public override void OnCancel(RequestMoq request)
        {
            _onCancel?.Invoke(request);
        }

        public override void OnComplete(RequestMoq request, ResponseMoq response)
        {
            _onComplete?.Invoke(request, response);
        }

        public override void OnError(RequestMoq request, string client, Exception ex)
        {
            _onError?.Invoke(request, client, ex);
        }

        public override void OnRemove(string id)
        {
            _onRemove?.Invoke(id);
        }

        public override void OnRead(string id)
        {
            _onRead?.Invoke(id);
        }
    }
}
