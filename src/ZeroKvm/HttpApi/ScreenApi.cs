using System.Collections.Immutable;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using ZeroKvm.Jpeg;

namespace ZeroKvm.HttpApi;

internal static class ScreenApi
{
    public static async Task GetScreenshotJpegAsync(Program program, HttpContext context, CancellationToken cancellationToken)
    {
        if (!TryParseJpegParams(context.Request.QueryString, out int quality, out JpegSubsampling subsampling))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        using JpegCompressor compressor = new()
        {
            Quality = quality,
            Subsampling = subsampling,
        };

        ReadOnlyMemory<byte> jpegBytes = await program.GetScreenshotAsync(compressor);

        context.Response.Headers.ContentType = "image/jpeg";
        context.Response.Headers.ContentLength = jpegBytes.Length;
        await context.ResponseBody.Stream.WriteAsync(jpegBytes, cancellationToken);
        await context.ResponseBody.Stream.FlushAsync(cancellationToken);
    }

    public static async Task GetScreenMjpegAsync(Program program, HttpContext context, CancellationToken cancellationToken)
    {
        MjpegStreamType? streamType = GetStreamType(context);
        if (streamType is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
            return;
        }

        if (!TryParseJpegParams(context.Request.QueryString, out int quality, out JpegSubsampling subsampling))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        using JpegCompressor compressor = new()
        {
            Quality = quality,
            Subsampling = subsampling,
        };

        switch (streamType.Value)
        {
            case MjpegStreamType.Multipart:
                string boundary = Guid.NewGuid().ToString();
                context.Response.Headers.ContentType = "multipart/x-mixed-replace; boundary=" + boundary;
                await context.ResponseBody.StartAsync(cancellationToken);
                await program.WriteMultipartStreamAsync(context.ResponseBody.Writer, boundary, compressor, cancellationToken);
                break;

            case MjpegStreamType.Rects:
                context.Response.Headers.ContentType = "image/mjpeg+rects";
                await context.ResponseBody.StartAsync(cancellationToken);
                await program.WriteRectsStreamAsync(context.ResponseBody.Writer, compressor, cancellationToken);
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return;
        }
    }

    private static bool TryParseJpegParams(string query, out int quality, out JpegSubsampling subsampling)
    {
        quality = 90;
        subsampling = JpegSubsampling.S444;

        foreach (var kv in new QueryStringEnumerable(query))
        {
            ReadOnlySpan<char> name = kv.DecodeName().Span;
            if (name.Equals("quality", StringComparison.OrdinalIgnoreCase))
            {
                if (!uint.TryParse(kv.DecodeValue().Span, out uint value) ||
                    value > 100)
                {
                    return false;
                }

                quality = (int)value;
            }
            else if (kv.DecodeName().Span.Equals("subsampling", StringComparison.OrdinalIgnoreCase))
            {
                subsampling = ParseSubsampling(kv.DecodeValue().Span);
                if (subsampling == JpegSubsampling.Unknown)
                {
                    return false;
                }
            }
        }

        return true;

        static JpegSubsampling ParseSubsampling(ReadOnlySpan<char> value)
        {
            return value switch
            {
                "420" => JpegSubsampling.S420,
                "422" => JpegSubsampling.S422,
                "440" => JpegSubsampling.S440,
                "444" => JpegSubsampling.S444,
                "gray" or "Gray" or "GRAY" => JpegSubsampling.Gray,
                _ => JpegSubsampling.Unknown,
            };
        }
    }

    private static MjpegStreamType? GetStreamType(HttpContext context)
    {
        foreach (string? accept in context.Request.Headers.Accept)
        {
            if (string.IsNullOrEmpty(accept) ||
                !MediaTypeHeaderValue.TryParseList(accept.Split(','), out IList<MediaTypeHeaderValue>? values))
            {
                return null;
            }

            foreach ((string mediaType, MjpegStreamType streamType) in _streamTypes)
            {
                foreach (MediaTypeHeaderValue value in values)
                {
                    if (value.MatchesMediaType(mediaType))
                    {
                        return streamType;
                    }
                }
            }
        }

        return MjpegStreamType.Multipart;
    }

    private static readonly ImmutableArray<(string MediaType, MjpegStreamType StreamType)> _streamTypes = [
        ("multipart/x-mixed-replace", MjpegStreamType.Multipart),
        ("image/mjpeg+rects", MjpegStreamType.Rects)
    ];
}
