using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ZeroKvm.HttpApi;

internal static class ApiUtils
{
    public static async Task<T?> ReadRequestBodyAsJsonAsync<T>(JsonTypeInfo<T> typeInfo, HttpContext context, int maxByteLength, CancellationToken cancellationToken)
            where T : class
    {
        ReadOnlyMemory<byte> requestBytes = await ReadRequestBodyAsync(context, maxByteLength, cancellationToken);
        if (requestBytes.Length == 0)
        {
            return null;
        }

        T? request;
        try
        {
            request = JsonSerializer.Deserialize(requestBytes.Span, typeInfo);
        }
        catch (Exception ex)
        {
            await RespondBadRequestAsync(ex is JsonException || ex is FormatException || ex is ArgumentException ? ex.Message : null, context, cancellationToken);
            return null;
        }

        if (request is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return null;
        }
        else if (request is IRequestValidation validation && validation.Validate() is { } error)
        {
            await RespondBadRequestAsync(error, context, cancellationToken);
            return null;
        }
        else
        {
            return request;
        }

        static async Task RespondBadRequestAsync(string? error, HttpContext context, CancellationToken cancellationToken)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            if (error is not null)
            {
                byte[] errorBytes = Encoding.UTF8.GetBytes(error);
                context.Response.Headers.ContentType = "text/plain";
                context.Response.Headers.ContentLength = errorBytes.Length;

                await context.ResponseBody.Writer.WriteAsync(errorBytes, cancellationToken);
            }
        }
    }

    public static async Task<ReadOnlyMemory<byte>> ReadRequestBodyAsync(HttpContext context, int maxLength, CancellationToken cancellationToken)
    {
        long? length = context.Request.Headers.ContentLength;
        if (length is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.LengthRequired;
            return Memory<byte>.Empty;
        }
        else if (length <= 0)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return Memory<byte>.Empty;
        }
        else if (length > maxLength)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
            return Memory<byte>.Empty;
        }

        byte[] buffer = new byte[(int)length];
        await context.Request.Body.ReadExactlyAsync(buffer, cancellationToken);
        return buffer;
    }

    public static async Task WriteResponseAsJsonAsync<T>(T value, JsonTypeInfo<T> typeInfo, HttpContext context, CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        context.Response.Headers.ContentLength = body.Length;
        context.Response.Headers.ContentType = "application/json; charset=utf-8";

        await context.ResponseBody.Stream.WriteAsync(body, cancellationToken);
    }
}
