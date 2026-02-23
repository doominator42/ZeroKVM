using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ZeroKvm.HttpApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace ZeroKvm;

internal class HttpApp : IHttpApplication<IFeatureCollection>, IDisposable
{
    private const string BasePath = "kvm/";
    private const string ViewerPath = BasePath + "viewer.html";

    private delegate Task ApiHandler(Program program, HttpContext context, CancellationToken cancellationToken);

    private static readonly FrozenDictionary<string, FrozenDictionary<string, ApiHandler>> _apiEndpoints = BuildEndpoints([
        (HttpMethods.Get, BasePath + "screenshot.jpg", ScreenApi.GetScreenshotJpegAsync),
        (HttpMethods.Get, BasePath + "screen.mjpeg", ScreenApi.GetScreenMjpegAsync),
        (HttpMethods.Post, BasePath + "keyboard", InputApi.PostKeyboardEventAsync),
        (HttpMethods.Get, BasePath + "keyboard/leds", InputApi.GetKeyboardLedsAsync),
        (HttpMethods.Post, BasePath + "pointer", InputApi.PostPointerEventAsync),
        (HttpMethods.Get, BasePath + "events", EventsApi.GetEventsAsync),
        (HttpMethods.Get, BasePath + "usb/state", UsbApi.GetStateAsync),
        (HttpMethods.Post, BasePath + "usb/attach", UsbApi.AttachAsync),
        (HttpMethods.Post, BasePath + "usb/detach", UsbApi.DetachAsync),
    ]);

    private static FrozenDictionary<string, FrozenDictionary<string, ApiHandler>> BuildEndpoints((string Method, string Path, ApiHandler Handler)[] endpointDefs)
    {
        Dictionary<string, Dictionary<string, ApiHandler>> endpoints = new();
        foreach ((string method, string path, ApiHandler handler) in endpointDefs)
        {
            if (!endpoints.TryGetValue(path, out Dictionary<string, ApiHandler>? values))
            {
                values = new();
                endpoints.Add(path, values);
            }

            values.Add(method, handler);
        }

        return endpoints.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToFrozenDictionary(), StringComparer.OrdinalIgnoreCase);
    }

    public record ListenOption(IPEndPoint EndPoint, SslProtocols SslProtocols);

    public record ProxyOption(string Path, Uri Destination)
    {
        public Uri MapToDestination(IHttpRequestFeature request)
        {
            return new UriBuilder(Destination)
            {
                Path = Destination.AbsolutePath + request.Path.Substring(Path.Length),
                Query = (QueryString.FromUriComponent(Destination.Query) + QueryString.FromUriComponent(request.QueryString)).ToUriComponent(),
            }.Uri;
        }
    }

    private static readonly CipherSuitesPolicy _tlsCipherSuites = new([
        TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
        TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256,
        TlsCipherSuite.TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
        TlsCipherSuite.TLS_DHE_RSA_WITH_CHACHA20_POLY1305_SHA256,

        TlsCipherSuite.TLS_AES_128_GCM_SHA256,
        TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
        TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
        TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_GCM_SHA256,

        TlsCipherSuite.TLS_AES_256_GCM_SHA384,
        TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384,
        TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
        TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_GCM_SHA384,
    ]);

    public static KestrelServer CreateServer(
        IEnumerable<ListenOption> listenOptions,
        X509Certificate2Collection? certificateFullChain)
    {
        ILoggerFactory logger = Logger.DebugLogEnabled ? new ConsoleLoggerFactory() : new NullLoggerFactory();
        KestrelServer httpServer = new(
            Options.Create(new KestrelServerOptions()
            {
                AddServerHeader = false,
                Limits =
                {
                    MaxConcurrentConnections = 64,
                    MaxRequestBodySize = 1024 * 1024,
                    MaxRequestBufferSize = 64 * 1024,
                    MinRequestBodyDataRate = null,
                },
            }),
            new SocketTransportFactory(
                Options.Create(new SocketTransportOptions()
                {
                    Backlog = 16,
                    MaxReadBufferSize = 64 * 1024,
                    MaxWriteBufferSize = 2 * 1024 * 1024,
                }),
                logger),
            logger);

        try
        {
            foreach (ListenOption listenOption in listenOptions)
            {
                httpServer.Options.Listen(listenOption.EndPoint, options =>
                {
                    if (listenOption.SslProtocols == SslProtocols.None)
                    {
                        options.Protocols = HttpProtocols.Http1;
                    }
                    else if (certificateFullChain is null)
                    {
                        throw new ArgumentException($"A certificate and private key must be specified with {ProgramOptions.CertificatePath.Name}/{ProgramOptions.CertificateKeyPath.Name} to enable TLS");
                    }
                    else
                    {
                        httpServer.Options.ApplicationServices ??= new HttpsServiceProvider(logger);

                        options.Protocols = HttpProtocols.Http1AndHttp2;
                        options.UseHttps(new HttpsConnectionAdapterOptions()
                        {
                            SslProtocols = listenOption.SslProtocols,
                            ServerCertificate = certificateFullChain[0],
                            ServerCertificateChain = certificateFullChain,
                            OnAuthenticate = (ctx, options) =>
                            {
                                options.CipherSuitesPolicy = _tlsCipherSuites;
                            },
                        });
                    }
                });
            }

            return httpServer;
        }
        catch
        {
            httpServer.Dispose();
            throw;
        }
    }

    public HttpApp(Program program, string? wwwrootPath, IEnumerable<ProxyOption> proxyOptions)
    {
        ArgumentNullException.ThrowIfNull(program);
        _program = program;
        _wwwrootPath = string.IsNullOrEmpty(wwwrootPath) ? null : Path.GetFullPath(wwwrootPath);
        _proxyPaths = [.. CreateProxyPaths(proxyOptions)];
        if (_proxyPaths.Length > 0)
        {
            _proxyClient = new(new SocketsHttpHandler()
            {
                AllowAutoRedirect = false,
                UseCookies = false,
            });
            _proxyClient.DefaultRequestHeaders.Clear();
            _proxyClient.Timeout = TimeSpan.FromSeconds(30);
        }

        static List<ProxyOption> CreateProxyPaths(IEnumerable<ProxyOption> options)
        {
            List<ProxyOption> paths = new();
            foreach (ProxyOption option in options)
            {
                paths.Add(new(option.Path, option.Destination));
            }

            return paths;
        }
    }

    private readonly Program _program;
    private readonly string? _wwwrootPath;
    private readonly ImmutableArray<ProxyOption> _proxyPaths;
    private readonly HttpClient? _proxyClient;

    public int? RedirectToHttpsPort { get; init; }

    public IFeatureCollection CreateContext(IFeatureCollection contextFeatures)
    {
        return contextFeatures;
    }

    public void DisposeContext(IFeatureCollection context, Exception? exception) { }

    public void Dispose()
    {
        _proxyClient?.Dispose();
    }

    public async Task ProcessRequestAsync(IFeatureCollection features)
    {
        try
        {
            HttpContext context = new(features);
            int httpsPort = RedirectToHttpsPort ?? 0;
            if (httpsPort > 0 && !context.Request.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                HandleHttpsRedirect(context, httpsPort);
                return;
            }

            string method = context.Request.Method;
            ReadOnlySpan<char> path = context.Request.Path.Trim('/');
            if (_apiEndpoints.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(path, out var endpointMethods))
            {
                if (endpointMethods.TryGetValue(method, out ApiHandler? handler))
                {
                    await handler(_program, context, context.RequestAborted);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                }
            }
            else if (TryGetFileAsset(_wwwrootPath, path, out FileInfo? file))
            {
                if (HttpMethods.IsGet(method))
                {
                    Wwwroot.Asset asset = await ReadFileAssetAsync(file, context.RequestAborted);
                    await WriteAssetAsync(context, asset, context.RequestAborted);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                }
            }
            else if (Wwwroot.TryGetAsset(path, out Wwwroot.Asset? asset))
            {
                if (HttpMethods.IsGet(method))
                {
                    await WriteAssetAsync(context, asset, context.RequestAborted);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                }
            }
            else if (path.Length == 0 || path.Equals(BasePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                if (HttpMethods.IsGet(method))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Found;
                    context.Response.Headers.Location = "/" + ViewerPath;
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                }
            }
            else
            {
                await HandleFallbackAsync(context);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            throw;
        }
    }

    private static string GetRequestHost(HttpContext context)
    {
        return context.Request.Headers.Host.ToString();
    }

    private static void HandleHttpsRedirect(HttpContext context, int httpsPort)
    {
        string rawTarget = context.Request.RawTarget;
        if (rawTarget.StartsWith('/'))
        {
            string host = GetRequestHost(context);
            context.Response.StatusCode = (int)HttpStatusCode.TemporaryRedirect;
            context.Response.Headers.Location = $"https://{(httpsPort == 443 ? new HostString(host) : new HostString(host, httpsPort))}{rawTarget}";
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
    }

    private Task HandleFallbackAsync(HttpContext context)
    {
        ReadOnlySpan<char> path = context.Request.Path;
        foreach (ProxyOption proxy in _proxyPaths)
        {
            if (path.StartsWith(proxy.Path, StringComparison.OrdinalIgnoreCase))
            {
                return HandleProxyAsync(context, proxy, context.RequestAborted);
            }
        }

        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        return Task.CompletedTask;
    }

    private static readonly FrozenSet<string> _proxyExcludedHeaders = new string[]
    {
        "accept-encoding",
        "connection",
        "keep-alive",
        "transfer-encoding",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private async Task HandleProxyAsync(HttpContext context, ProxyOption proxyOption, CancellationToken cancellationToken)
    {
        HttpClient client = _proxyClient ?? throw new InvalidOperationException();

        using HttpRequestMessage upstreamRequest = new()
        {
            Method = HttpMethod.Parse(context.Request.Method),
            RequestUri = proxyOption.MapToDestination(context.Request),
            Content = new StreamContent(context.Request.Body),
        };

        foreach (var header in context.Request.Headers)
        {
            if (!_proxyExcludedHeaders.Contains(header.Key))
            {
                if (!upstreamRequest.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value))
                {
                    upstreamRequest.Content.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value);
                }
            }
        }

        IHttpConnectionFeature connection = context.Features.GetRequiredFeature<IHttpConnectionFeature>();
        if (connection.RemoteIpAddress?.ToString() is { } remoteIp)
        {
            upstreamRequest.Headers.Add("X-Forwarded-For", remoteIp);
            upstreamRequest.Headers.Add("X-Forwarded-Proto", context.Request.Scheme);
            upstreamRequest.Headers.Add("X-Real-IP", remoteIp);
        }

        try
        {
            using HttpResponseMessage upstreamResponse = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            context.Response.ReasonPhrase = upstreamResponse.ReasonPhrase;
            foreach (var header in upstreamResponse.Headers)
            {
                if (!_proxyExcludedHeaders.Contains(header.Key))
                {
                    context.Response.Headers[header.Key] = new StringValues(header.Value.ToArray());
                }
            }

            foreach (var header in upstreamResponse.Content.Headers)
            {
                context.Response.Headers[header.Key] = new StringValues(header.Value.ToArray());
            }

            await upstreamResponse.Content.CopyToAsync(context.ResponseBody.Stream, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
            }
        }
    }

    private static bool TryGetFileAsset([NotNullWhen(true)] string? wwwrootPath, ReadOnlySpan<char> path, [NotNullWhen(true)] out FileInfo? file)
    {
        file = null;
        if (wwwrootPath is null)
        {
            return false;
        }

        string fullPath = Path.GetFullPath(path.Trim('/').ToString(), wwwrootPath);
        if (!fullPath.StartsWith(wwwrootPath))
        {
            return false;
        }

        file = new(fullPath);
        return file.Exists;
    }

    private static async Task<Wwwroot.Asset> ReadFileAssetAsync(FileInfo file, CancellationToken cancellationToken)
    {
        byte[] content = await File.ReadAllBytesAsync(file.FullName, cancellationToken);
        return new(file, content);
    }

    private static async Task WriteAssetAsync(HttpContext context, Wwwroot.Asset asset, CancellationToken cancellationToken)
    {
        // TODO: cache
        SetHeaders(context.Response.Headers, asset);

        await context.ResponseBody.StartAsync(cancellationToken);
        PipeWriter body = context.ResponseBody.Writer;
        asset.Content.Span.CopyTo(body.GetSpan(asset.Content.Length));
        body.Advance(asset.Content.Length);

        await body.FlushAsync(cancellationToken);

        static void SetHeaders(IHeaderDictionary headers, Wwwroot.Asset asset)
        {
            headers.ContentLength = asset.Content.Length;
            headers.ContentType = asset.MediaType + "; charset=utf-8";
            headers.LastModified = asset.ModifiedDate.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'");
        }
    }
}
