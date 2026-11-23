using System.Net;
using System.Text;

namespace ProjectManager.Tests.Services.GitBackfill
{
    /// <summary>
    /// Egy elkapott kimenő kérés lényege. Nem a HttpRequestMessage példányt tartjuk meg:
    /// azt a HttpClient a válasz után felszabadíthatja, és akkor a tesztben olvasott fejlécek
    /// a valóságtól elszakadhatnának.
    /// </summary>
    internal sealed record CapturedRequest(string Url, IReadOnlyDictionary<string, string> Headers)
    {
        public string? Header(string name) =>
            Headers.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// Kézi HTTP válaszadó a szolgáltatói API helyén.
    ///
    /// Így a lekérő réteg Docker és hálózat nélkül tesztelhető - ami itt nem kényelmi kérdés:
    /// a valódi szolgáltató megkérdezése kvótát fogyasztana, és a hibaágak (401, 403 kifogyott
    /// kvótával, 500, időtúllépés) élesben egyáltalán nem előállíthatók szándékosan.
    /// </summary>
    internal sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<CapturedRequest, HttpResponseMessage> _respond;

        public List<CapturedRequest> Requests { get; } = [];

        public StubHttpMessageHandler(Func<CapturedRequest, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        /// <summary>Ugyanaz a válasz minden kérésre.</summary>
        public static StubHttpMessageHandler Returning(
            string json, HttpStatusCode status = HttpStatusCode.OK,
            params (string Name, string Value)[] headers) =>
            new(_ => Response(json, status, headers));

        /// <summary>A válasz a kérés sorszámától függ (lapozás).</summary>
        public static StubHttpMessageHandler Paging(Func<int, string> jsonForPage)
        {
            var page = 0;
            return new StubHttpMessageHandler(_ => Response(jsonForPage(++page), HttpStatusCode.OK));
        }

        public static HttpResponseMessage Response(
            string json, HttpStatusCode status, params (string Name, string Value)[] headers)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            foreach (var (name, value) in headers)
                response.Headers.TryAddWithoutValidation(name, value);

            return response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers
                .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

            var captured = new CapturedRequest(request.RequestUri!.ToString(), headers);
            Requests.Add(captured);

            return Task.FromResult(_respond(captured));
        }
    }

    internal sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        //A kezelőt nem a kliens birtokolja: egy teszt több klienst is kérhet ugyanarra
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
