using Microsoft.AspNetCore.Http.Features;

namespace ZeroKvm;

internal sealed class HttpContext
{
    public HttpContext(IFeatureCollection features)
    {
        ArgumentNullException.ThrowIfNull(features);
        Features = features;
        Request = features.GetRequiredFeature<IHttpRequestFeature>();
        Response = features.GetRequiredFeature<IHttpResponseFeature>();
        ResponseBody = features.GetRequiredFeature<IHttpResponseBodyFeature>();
        RequestAborted = features.GetRequiredFeature<IHttpRequestLifetimeFeature>().RequestAborted;
    }

    public IFeatureCollection Features { get; }

    public IHttpRequestFeature Request { get; }

    public IHttpResponseFeature Response { get; }

    public IHttpResponseBodyFeature ResponseBody { get; }

    public CancellationToken RequestAborted { get; }
}
