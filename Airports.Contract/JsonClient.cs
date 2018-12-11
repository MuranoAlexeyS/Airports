using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Airports.Contract.Interfaces;
using Newtonsoft.Json;

namespace Airports.Contract
{
    public class JsonClient : IHttpJsonClient
    {
        private readonly HttpClient _baseHttp;

        public JsonClient(HttpClient baseHttp)
        {
            this._baseHttp = baseHttp;
        }
        public async Task<T> GetAsync<T>(CancellationToken token, Uri uri)
        {
            using (var reqM = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                try
                {
                    var resM = await _baseHttp.SendAsync(reqM, token).ConfigureAwait(false);
                    if (resM.IsSuccessStatusCode)
                    {
                        var data = await resM.Content.ReadAsStringAsync();
                        return await Task.Factory.StartNew<T>(() => JsonConvert.DeserializeObject<T>(data));
                    }
                    resM.EnsureSuccessStatusCode();
                    return default(T);
                }
                catch (OperationCanceledException)
                {
                    return default(T);
                }
            }
        }
    }
}
