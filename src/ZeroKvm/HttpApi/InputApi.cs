using System.Net;
using ZeroKvm.Hid;

namespace ZeroKvm.HttpApi;

internal static class InputApi
{
    public static async Task PostKeyboardEventAsync(Program program, HttpContext context, CancellationToken cancellationToken)
    {
        KeyboardEventRequest? request = await ApiUtils.ReadRequestBodyAsJsonAsync(JsonContext.Default.KeyboardEventRequest, context, 64 * 1024, cancellationToken);
        if (request is null)
        {
            return;
        }

        KeyScan[] keyScans = new KeyScan[request.Keys.Length];
        for (int i = 0; i < keyScans.Length; i++)
        {
            KeyboardEventRequest.KeyEvent keyEvent = request.Keys[i];
            keyScans[i] = new(keyEvent.ScanCode, keyEvent.IsDown, keyEvent.Delay);
        }

        bool success = await program.SendBootKeyboardScansAsync(keyScans, request.Reset, cancellationToken);
        context.Response.StatusCode = success ? (int)HttpStatusCode.NoContent : (int)HttpStatusCode.InternalServerError;
    }

    public static async Task GetKeyboardLedsAsync(Program program, HttpContext context, CancellationToken cancellationToken)
    {
        HidLedsReport report = program.LastLedsReport;
        KeyboardLedsResponse response = new()
        {
            NumLock = report.HasFlag(HidLedsReport.NumLock),
            CapsLock = report.HasFlag(HidLedsReport.CapsLock),
            ScrollLock = report.HasFlag(HidLedsReport.ScrollLock),
            Compose = report.HasFlag(HidLedsReport.Compose),
            Kana = report.HasFlag(HidLedsReport.Kana),
        };

        await ApiUtils.WriteResponseAsJsonAsync(response, JsonContext.Default.KeyboardLedsResponse, context, cancellationToken);
    }

    public static async Task PostPointerEventAsync(Program program, HttpContext context, CancellationToken cancellationToken)
    {
        PointerEventRequest? request = await ApiUtils.ReadRequestBodyAsJsonAsync(JsonContext.Default.PointerEventRequest, context, 64 * 1024, cancellationToken);
        if (request is null)
        {
            return;
        }

        PointerEvent[] events = new PointerEvent[request.Events.Length];
        for (int i = 0; i < events.Length; i++)
        {
            PointerEventRequest.Event @event = request.Events[i];
            events[i] = new(
                downButtons: GetButtons(@event, true),
                upButtons: GetButtons(@event, false),
                move: @event.X is null || @event.Y is null ? null : (@event.X.Value, @event.Y.Value),
                wheel: @event.Wheel ?? 0,
                delay: @event.Delay);
        }

        bool? success = request.Type switch
        {
            PointerType.BootMouse => await program.SendBootMouseEventsAsync(events, request.Reset, cancellationToken),
            PointerType.AbsoluteMouse => await program.SendAbsoluteMouseEventsAsync(events, request.Reset, cancellationToken),
            _ => null,
        };

        context.Response.StatusCode = success is null ?
            (int)HttpStatusCode.BadRequest :
            success.Value ?
                (int)HttpStatusCode.NoContent :
                (int)HttpStatusCode.InternalServerError;

        static PointerButtons GetButtons(PointerEventRequest.Event @event, bool state)
        {
            return (@event.Left == state ? PointerButtons.Left : 0) |
                (@event.Middle == state ? PointerButtons.Middle : 0) |
                (@event.Right == state ? PointerButtons.Right : 0);
        }
    }
}
