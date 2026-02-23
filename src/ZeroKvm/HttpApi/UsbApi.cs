using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace ZeroKvm.HttpApi;

internal static class UsbApi
{
    private static async Task WriteStateResponseAsync(Udc.State state, HttpContext context, CancellationToken cancellationToken)
    {
        UsbStateResponse response = new()
        {
            Attached = state.IsAttachedState(),
        };

        await ApiUtils.WriteResponseAsJsonAsync(response, JsonContext.Default.UsbStateResponse, context, cancellationToken);
    }

    public static async Task GetStateAsync(Program program, HttpContext context, CancellationToken cancellationToken)
    {
        await WriteStateResponseAsync(Udc.GetState(program.UdcName), context, cancellationToken);
    }

    public static async Task AttachAsync(Program program, HttpContext context, CancellationToken cancellationToken)
    {
        await AttachOrDetachAsync(true, program, context, cancellationToken);
    }

    public static async Task DetachAsync(Program program, HttpContext context, CancellationToken cancellationToken)
    {
        await AttachOrDetachAsync(false, program, context, cancellationToken);
    }

    private static async Task AttachOrDetachAsync(bool attach, Program program, HttpContext context, CancellationToken cancellationToken)
    {
        if (!ParseTimeoutParam(context.Request.QueryString, out TimeSpan? timeout))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        Udc.State state = Udc.GetState(program.UdcName);
        if (state.IsAttachedState() != attach && (timeout is null || timeout.Value.Ticks > 0))
        {
            if (attach && state == Udc.State.NotAttached && program.UsbGadget.Udc.Length == 0)
            {
                Logger.LogDebug(static udc => $"Setting UDC to {udc}", program.UdcName);
                program.UsbGadget.Udc = program.UdcName;
            }
            else
            {
                Logger.LogDebug(static attach => attach ? "Attaching USB" : "Detaching USB", attach);
                Udc.SoftConnect(program.UdcName, attach);
            }

            await Udc.WaitForStateAsync(program.UdcName, state => state.IsAttachedState() == attach, timeout ?? Timeout.InfiniteTimeSpan, cancellationToken);
        }

        await WriteStateResponseAsync(state, context, cancellationToken);
    }

    private static bool ParseTimeoutParam(string query, out TimeSpan? timeout)
    {
        timeout = null;

        foreach (var kv in new QueryStringEnumerable(query))
        {
            ReadOnlySpan<char> name = kv.DecodeName().Span;
            if (name.Equals("timeout", StringComparison.OrdinalIgnoreCase))
            {
                if (!uint.TryParse(kv.DecodeValue().Span, out uint value))
                {
                    return false;
                }

                timeout = TimeSpan.FromMilliseconds(value);
            }
        }

        return true;
    }
}
