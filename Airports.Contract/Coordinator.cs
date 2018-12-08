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
        public Coordinator(IClientBalancer<T, V> clients)
        {
            this._clients = clients;
        }

        private readonly IClientBalancer<T, V> _clients;
        private readonly ConcurrentDictionary<string, Token<V>> _counters = new ConcurrentDictionary<string, Token<V>>();

        public async Task<string> GetToken(T request)
        {
            var id = request.GetIdentifier();
            var newToken = new Token<V>(id);
            newToken.IsRequested();
            var token = _counters.GetOrAdd(id, newToken);
            bool added = false;
            added = token.Equals(newToken) || token.IsRequested();
            if (!added)
            {
                if (token.IsNotRemoved())
                {
                    token.BlockingAction(() =>
                    {
                        token = _counters.AddOrUpdate(id, newToken, (x, o) => newToken);
                        if (token.Equals(newToken))
                        {
                            OnRemove(id);
                        }
                    });
                }
                else
                {
                    token.BlockingAction(() =>
                    {
                        token = _counters.GetOrAdd(id, newToken);
                        if (token.Equals(newToken))
                        {
                            OnAdd(request);
                        }
                    });
                }
            }
            else
            {
                OnAdd(request);
            }
            RunClient(token, request);
            return await Task.FromResult(id); ;
        }

        public async Task Cancel(string id)
        {
            Token<V> token = null;
            _counters.TryGetValue(id, out token);
            if (token != null)
            {
                if (token.IsCancel(false))
                {
                    TryRemove(token, id);
                }
            }
            await Task.CompletedTask;
        }

        public async Task<Response<V>> Result(string id)
        {
            Task<Response<V>> tr;
            Token<V> token = null;
            _counters.TryGetValue(id, out token);
            if (token != null)
            {
                tr = Task.FromResult(new Response<V>(ResponseState.Processed, default(V)));
                if (token.Data != null)
                {
                    tr = Task.FromResult(new Response<V>(ResponseState.Readed, token.Data));
                    if (token.IsReaded())
                    {
                        if (!TryRemove(token, id))
                        {
                            tr = Task.FromResult(new Response<V>(ResponseState.Processed, default(V)));
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
        private void RunClient(Token<V> token, T request)
        {
            if (token.Init())
            {
                Task.Run(async () =>
                {
                    var client = _clients.GetNext();
                    try
                    {
                        var tr = client.AskAsync(request, token.CancelToken);
                        token.Data = await tr;
                        OnComplete(request, token.Data);
                    }
                    catch (TaskCanceledException)
                    {
                        CancelError(token, request);
                        OnCancel(request);
                    }
                    catch (Exception ex)
                    {
                        CancelError(token, request);
                        OnError(request, client.ClientName, ex);
                    }
                }).ConfigureAwait(false);
            }
        }

        private void CancelError(Token<V> token, T request)
        {
            string id = request.GetIdentifier();
            token.IsCancel(true);
            TryRemove(token, id);
        }
        private bool TryRemove(Token<V> token, string id)
        {
            bool can = token.IsNotRemoved();
            if (can)
            {
                token.BlockingAction(() =>
                {
                    if (_counters.TryRemove(id, out _))
                    {
                        OnRemove(id);
                    }
                });
            }
            return can;
        }
        public virtual void OnCancel(T request) { }
        public virtual void OnRead(string id) { }
        public virtual void OnError(T req, string client, Exception ex) { }
        public virtual void OnRemove(string id) { }
        public virtual void OnAdd(T request) { }
        public virtual void OnComplete(T request, V response) { }

        private class Token<V>
        {
            internal Token(string token)
            {
                _source = new CancellationTokenSource();
                CancelToken = _source.Token;
                _token = token;
            }

            private readonly string _token;
            private readonly CancellationTokenSource _source;
            private int _flag = 0;
            private int _counter = 0;
            private int _lockCounter = 0;
            private int _setter = 0;
            private V _data = default(V);
            private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

            internal V Data
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
            internal CancellationToken CancelToken { get; }

            internal bool Init()
            {
                var x = Interlocked.CompareExchange(ref _flag, 1, 0) == 0;
                if (x)
                {
                    return true;
                }
                return false;
            }
            internal bool IsRequested()
            {
                try
                {
                    return CounterLockAction(() =>
                    {
                        if (Interlocked.Increment(ref _counter) > 0)
                        {
                            return true;
                        }
                        else
                        {
                            Interlocked.Decrement(ref _counter);
                            return false;
                        }
                    }, false);
                }
                finally
                {
                    Interlocked.CompareExchange(ref _lockCounter, 0, 1);
                }

            }
            internal bool IsReaded()
            {
                bool result = false;
                try
                {
                    result = CounterLockAction(() =>
                    {
                        return Interlocked.Decrement(ref _counter) == 0;
                    }, true);
                }
                finally
                {
                    if (!result)
                        Interlocked.CompareExchange(ref _lockCounter, 0, 1);
                }
                return result;
            }

            internal bool IsNotRemoved()
            {
                bool x = Interlocked.Exchange(ref _flag, -1) >= 0;
                if (x)
                {
                    return true;
                }
                return false;
            }
            internal bool IsCancel(bool isError)
            {
                if (isError)
                {
                    return CounterLockAction(() =>
                    {
                        return Interlocked.Exchange(ref _counter, 0) > 0;
                    }, true);
                }
                else
                {
                    if (IsReaded())
                    {
                        _source.Cancel();
                        return true;
                    }
                }
                return false;
            }
            internal void BlockingAction(Action action)
            {
                _lock.EnterWriteLock();
                try
                {
                    action();
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            private bool CounterLockAction(Func<bool> action, bool deletedReturn)
            {
                bool success;
                if (Interlocked.CompareExchange(ref _lockCounter, 1, 0) == 0)
                {
                    success = action();
                }
                else
                {
                    while (true)
                    {
                        if (SpinWait.SpinUntil(() => Interlocked.CompareExchange(ref _lockCounter, 1, 0) == 0, 1))
                        {
                            success = action();
                            break;
                        }
                        else
                        {
                            Thread.Yield();
                        }
                        if (Interlocked.CompareExchange(ref _flag, -1, -1) == -1)
                            return deletedReturn;
                    }
                }
                return success;
            }
            ~Token()
            {
                if (_lock != null) _lock.Dispose();
                if (_source != null) _source.Dispose();
            }
        }
    }
}
