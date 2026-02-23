using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace ZeroKvm;

// This provides the required services for a kestrel server with HTTPS enabled.
internal sealed class HttpsServiceProvider : IServiceProvider
{
    public HttpsServiceProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _kestrelMetrics = CreateKestrelMetrics(new DummyMeterFactory());
    }

    private readonly ILoggerFactory _loggerFactory;
    private readonly object _kestrelMetrics;

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(ILoggerFactory))
        {
            return _loggerFactory;
        }
        else if (serviceType == _kestrelMetrics.GetType())
        {
            return _kestrelMetrics;
        }
        else
        {
            return null;
        }
    }

    // Hack to create an instance of KestrelMetrics, the HTTPS connection middleware won't work if not registered
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.KestrelMetrics, Microsoft.AspNetCore.Server.Kestrel.Core")]
    private static extern object CreateKestrelMetrics(IMeterFactory meterFactory);

    private sealed class DummyMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);

        public void Dispose() { }
    }
}
