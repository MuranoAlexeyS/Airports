using Airports.Contract.Interfaces;
using Airports.Contract.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Airports.Contract
{
    public abstract class ReduceCoordinator<T, V> : ICoordinator<T, Response<V>> where T : class, IRequest
    {
        public ReduceCoordinator(IDispatcher<T, V> clients, TimeSpan expirate)
        {
            this._clients = clients;
            this._expirate = expirate;
            _removeAction = (x, y) => TryRemove(x, y);
        }

        private readonly IDispatcher<T, V> _clients;
        private readonly ConcurrentDictionary<string, Token<V>> _counters = new ConcurrentDictionary<string, Token<V>>();
        private readonly Func<Token<V>, string, bool> _removeAction;
        private readonly TimeSpan _expirate;

        public async Task<string> GetToken(T request)
        {
            var hashId = request.GetIdentifier();
            var newToken = new Token<V>(hashId, _expirate, _removeAction);
            newToken.IsRequested();
            var token = _counters.GetOrAdd(hashId, newToken);
            bool added = false;
            added = token.Equals(newToken) || token.IsRequested();
            if (!added)
            {
                if (token.IsNotRemoved())
                {
                    token.BlockingAction(() =>
                    {
                        token = _counters.AddOrUpdate(hashId, newToken, (x, o) =>
                        {
                            o.Dispose();
                            return newToken;
                        });
                        if (token.Equals(newToken))
                        {
                            OnRemove(hashId);
                        }
                    });
                }
                else
                {
                    token.BlockingAction(() =>
                    {
                        token = _counters.GetOrAdd(hashId, newToken);
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
            await RunClient(token, request);
            return await Task.FromResult(hashId);
        }

        public async Task Cancel(string hashId)
        {
            Token<V> token = null;
            _counters.TryGetValue(hashId, out token);
            if (token != null)
            {
                if (token.IsCancel(false))
                {
                    TryRemove(token, hashId);
                }
            }
            await Task.CompletedTask;
        }

        public async Task<Response<V>> Result(string hashId)
        {
            Task<Response<V>> tr;
            Token<V> token = null;
            _counters.TryGetValue(hashId, out token);
            if (token != null)
            {
                tr = Task.FromResult(new Response<V>(ResponseState.Processed, default(V)));
                if (token.Data != null)
                {
                    tr = Task.FromResult(new Response<V>(ResponseState.Readed, token.Data));
                    if (token.IsReaded())
                    {
                        if (!TryRemove(token, hashId))
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
        public async Task<V> ResultAsync(string hashId)
        {
            Token<V> token = null;
            _counters.TryGetValue(hashId, out token);
            if (token != null)
            {
                var data = await token.Awaiting();
                OnRead(hashId);
                if (token.IsReaded())
                {
                    TryRemove(token, hashId);
                    OnRemove(hashId);
                }
                return data;
            }
            return await Task.FromResult(default(V));
        }
        private async Task RunClient(Token<V> token, T request)
        {
            if (token.Init())
            {
                token.Awaiter = Task.Run(async () =>
                  {
                      var client = _clients.GetNext();
                      try
                      {
                          var tr = client.AskAsync(request, token.CancelToken).ConfigureAwait(false);
                          token.Data = await tr;
                          OnComplete(request, token.Data);
                      }
                      catch (OperationCanceledException)
                      {
                          CancelError(token, request);
                          OnCancel(request);
                      }
                      catch (Exception ex)
                      {
                          CancelError(token, request);
                          OnError(request, client.ClientName, ex);
                      }
                  });
            }
            await Task.CompletedTask;
        }

        private void CancelError(Token<V> token, T request)
        {
            string hashId = request.GetIdentifier();
            token.IsCancel(true);
            TryRemove(token, hashId);
        }
        private bool TryRemove(Token<V> token, string hashId)
        {
            bool can = token.IsNotRemoved();
            if (can)
            {
                token.BlockingAction(() =>
                {
                    _counters.TryRemove(hashId, out _);
                });
                OnRemove(hashId);
                token.Dispose();
            }
            return can;
        }
        public virtual void OnCancel(T request) { }
        public virtual void OnRead(string hashId) { }
        public virtual void OnError(T req, string client, Exception ex) { }
        public virtual void OnRemove(string hashId) { }
        public virtual void OnAdd(T request) { }
        public virtual void OnComplete(T request, V response) { }

        private class Token<V> : IDisposable
        {
            internal Token(string hashId, TimeSpan expire, Func<Token<V>, string, bool> removeAction)
            {
                _source = new CancellationTokenSource();
                CancelToken = _source.Token;
                _id = hashId;
                _timer = new System.Timers.Timer(expire.TotalMilliseconds);
                _timer.AutoReset = false;
                _timer.Elapsed += (s, e) =>
                {
                    if (_setter == 1)
                    {
                        removeAction(this, _id);
                        _timer.Stop();
                    }
                    else
                    { _timer.Start(); }
                };
                _timer.Start();
            }
            private Task _awaiter;
            private readonly string _id;
            private readonly System.Timers.Timer _timer;
            private readonly CancellationTokenSource _source;
            private int _flag = 0;
            private int _counter = 0;
            private int _lockCounter = 0;
            private int _setter = 0;
            private V _data = default(V);
            private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

            internal V Data
            {
                get => _data;
                set => _data = Interlocked.CompareExchange(ref _setter, 1, 0) == 0 ? value : _data;
            }
            internal CancellationToken CancelToken { get; }

            public Task Awaiter { set => _awaiter = value; }

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
            internal async Task<V> Awaiting()
            {
                await _awaiter;
                return await Task.FromResult(_data);
            }
            public void Dispose()
            {
                _lock?.Dispose();
                _source?.Dispose();
                _timer?.Dispose();
            }
        }
    }
}
