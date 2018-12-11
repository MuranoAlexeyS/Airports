using Airports.Contract.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Airports.Contract
{
    public class ThrottledJsonClient
    {
        private int _startLock;
        public ThrottledJsonClient(int maxCountPerSecond, IHttpJsonClient httpClient, ILogger logger)
        {
            this._tcs = new TaskCompletionSource<int>();
            this._maxCountPerSecond = maxCountPerSecond;
            this._httpClient = httpClient;
            this._semaphore = new SemaphoreSlim(maxCountPerSecond);
            this._logger = logger;
        }
        private readonly SemaphoreSlim _semaphore;
        private readonly ILogger _logger;
        private readonly TaskCompletionSource<int> _tcs;
        private readonly int _maxCountPerSecond;
        private readonly IHttpJsonClient _httpClient;

        private async Task<V> ThrottledGetJsonAsync<V>(CancellationToken token, Uri uri)
        {
            await _semaphore.WaitAsync(token);
            return await _httpClient.GetAsync<V>(token, uri);
        }
        public async void Start()
        {
            await PeriodicallyReleaseAsync(_tcs.Task);
            Interlocked.Exchange(ref _startLock, 0);
        }
        public void Stop()
        {
            _tcs.SetResult(0);
        }
        private async Task PeriodicallyReleaseAsync(Task stop)
        {
            if (Interlocked.Exchange(ref _startLock, 1) == 0)
            {
                try
                {
                    while (true)
                    {
                        var timer = Task.Delay(TimeSpan.FromSeconds(1.05));
                        if (await Task.WhenAny(timer, stop) == stop)
                            return;

                        for (int i = 0; i != _maxCountPerSecond; ++i)
                        {
                            try
                            {
                                _semaphore.Release();
                            }
                            catch (SemaphoreFullException)
                            {
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.Exchange(ref _startLock, 0);
                    _logger.LogError(ex, "Throttled process error");
                }
            }
        }
    }
}

