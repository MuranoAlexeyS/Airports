using Airports.Contract.Interfaces;
using Airports.Contract.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Airports.Contract
{
    public abstract class Coordinator<T, V> : ICoordinator<T, Response<V>> where T : class, IRequest
    {
        private readonly IClientBalancer<T, V> _clients;

        public Coordinator(IClientBalancer<T, V> clients)
        {
            this._clients = clients;
        }
        private readonly ConcurrentDictionary<string, Token<V>> _counters = new ConcurrentDictionary<string, Token<V>>();
        public async Task Cancel(string id)
        {
            Token<V> token = null;
            _counters.TryGetValue(id, out token);
            if (token != null)
            {
                if (token.Cancel())
                {
                    if (token.NotRemoved())
                    {
                        _counters.TryRemove(id, out token);
                    }
                }
            }
            await Task.CompletedTask;
        }

        public async Task<string> GetToken(T request)
        {
            var id = request.GetIdentifier();
            Task<string> tr = Task.FromResult(id);
            var token = _counters.GetOrAdd(id, new Token<V>(id));
            if (!token.Requested())
            {
                token = _counters.AddOrUpdate(id, new Token<V>(id), (i, o) =>
                {
                    o.NotRemoved();
                    return new Token<V>(id);
                });
            }
            RunClient(token, request);
            return await tr;
        }

        private void RunClient(Token<V> token, T request)
        {
            if (token.Init())
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var tr = _clients.GetNext().AskAsync(request, token.CancelToken);
                        token.Data = await tr;
                    }
                    catch (TaskCanceledException)
                    {
                        token.Cancel();
                        if (token.NotRemoved())
                        {
                            _counters.TryRemove(request.GetIdentifier(), out token);
                            token.Dispose();
                        }
                    }
                }).ConfigureAwait(false);
            }
        }

        public async Task<Response<V>> Result(string id)
        {
            Task<Response<V>> tr;
            Token<V> token = null;
            _counters.TryGetValue(id, out token);
            if (token != null)
            {
                if (token.Data == null)
                {
                    tr = Task.FromResult(new Response<V>(ResponseState.Processed, default(V)));
                }
                else
                {
                    tr = Task.FromResult(new Response<V>(ResponseState.Readed, token.Data));
                    if (token.Readed())
                    {
                        if (token.NotRemoved())
                        {
                            _counters.TryRemove(id, out token);
                            token.Dispose();
                        }
                    }
                }
            }
            else
            {
                tr = Task.FromResult(new Response<V>(ResponseState.Deleted, default(V)));
            }
            return await tr;
        }
        private class Token<V> : IDisposable
        {
            private readonly string _token;
            private readonly CancellationTokenSource _source;
            private int _flag = 0;
            private int _counter = 0;
            private int _setter = 0;
            private V _data = default(V);
            public V Data
            {
                get { return _data; }
                set
                {
                    if (Interlocked.Exchange(ref _setter, 1) == 0)
                    {
                        _data = value;
                    }
                }
            }

            public CancellationToken CancelToken { get; }

            public Token(string token)
            {
                _source = new CancellationTokenSource();
                CancelToken = _source.Token;
                _token = token;
            }
            public void Dispose()
            {
                _source.Dispose();
            }

            public bool Init()
            {
                return Interlocked.Exchange(ref _flag, 1) == 0;
            }

            public bool Requested()
            {
                int notdelted = Interlocked.CompareExchange(ref _counter, 1, 0);
                if (notdelted >= 0)
                {
                    if (notdelted == 1)
                    {
                        Interlocked.Increment(ref _counter);
                    }
                    return true;
                }
                return false;
            }

            public bool Readed()
            {
                if (Interlocked.Decrement(ref _counter) <= 0)
                {
                    return Interlocked.CompareExchange(ref _counter, -1, 0) <= 0;
                }
                return false;
            }
            public bool NotRemoved()
            {
                return Interlocked.Exchange(ref _flag, -1) >= 0;
            }
            public bool Cancel()
            {
                if (Readed())
                {
                    _source.Cancel();
                    return true;
                }
                return false;
            }
        }
    }
}
