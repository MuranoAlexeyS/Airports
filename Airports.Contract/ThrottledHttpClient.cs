using Airports.Contract.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Airports.Contract
{
    public class ThrottledJsonClient
    {
        public ThrottledJsonClient(int maxCountPerSecond, IHttpJsonClient httpClient)
        {
            this._maxCountPerSecond = maxCountPerSecond;
            this._httpClient = httpClient;
        }
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(10);
        private readonly int _maxCountPerSecond;
        private readonly IHttpJsonClient _httpClient;

        private async Task<V> ThrottledGetJsonAsync<V>(CancellationToken token, Uri uri)
        {
            await _semaphore.WaitAsync(token);
            return await _httpClient.GetAsync<V>(token, uri);
        }

        private async Task PeriodicallyReleaseAsync(Task stop)
        {
            while (true)
            {
                var timer = Task.Delay(TimeSpan.FromSeconds(1.2));

                if (await Task.WhenAny(timer, stop) == stop)
                    return;

                // Release the semaphore at most 10 times.
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
    }
}
        
