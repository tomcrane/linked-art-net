
namespace PmcTransformer.Reconciliation
{
    internal class HttpClients
    {
        private static readonly HttpClient _httpClient;

        static HttpClients()
        {            
            _httpClient = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15)
            });
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Paul Mellon Centre Linked Art Client");
        }

        public static HttpClient GetStandardClient()
        {
            return _httpClient;
        }
    }
}
