using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LeccionesAprendidas.Tests.Helpers;

public class FakeHttpRequestData : HttpRequestData
{
    private readonly MemoryStream _body;
    private readonly HttpHeadersCollection _headers = new();

    public FakeHttpRequestData(string body = "") : base(BuildFakeContext())
    {
        _body = new MemoryStream(Encoding.UTF8.GetBytes(body));
    }

    internal static FunctionContext BuildFakeContext()
    {
        var workerOptions = new WorkerOptions { Serializer = new JsonObjectSerializer() };

        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(workerOptions));
        var provider = services.BuildServiceProvider();

        return new TestFunctionContext(provider);
    }

    public static FakeHttpRequestData WithJson(object? content)
    {
        var json = JsonSerializer.Serialize(content);
        return new FakeHttpRequestData(json);
    }

    public override Stream Body => _body;
    public override HttpHeadersCollection Headers => _headers;
    public override IReadOnlyCollection<IHttpCookie> Cookies => Array.Empty<IHttpCookie>();
    public override Uri Url => new Uri("https://localhost/api/test");
    public override IEnumerable<ClaimsIdentity> Identities => Enumerable.Empty<ClaimsIdentity>();
    public override string Method => "POST";

    public override HttpResponseData CreateResponse() => new FakeHttpResponseData(FunctionContext);
}
