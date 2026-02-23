using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace ZeroKvm.HttpApi;

internal static class EventsApi
{
    private const int PollingTimeoutSeconds = 30;

    public static async Task GetEventsAsync(Program program, HttpContext context, CancellationToken cancellationToken)
    {
        if (!ParseQueueIdParam(context.Request.QueryString, out Guid? queueId))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        EventBus.Consumer consumer = program.GetEventBusConsumer(queueId, out bool isNewConsumer);
        if (isNewConsumer)
        {
            await ApiUtils.WriteResponseAsJsonAsync(
                new QueueCreatedResponse()
                {
                    QueueId = consumer.Id,
                },
                JsonContext.Default.QueueCreatedResponse,
                context,
                cancellationToken);

            return;
        }

        List<IEvent> events = new(2);
        while (consumer.TryGetNextEvent(out object? @event))
        {
            events.Add((IEvent)@event);
        }

        if (events.Count == 0)
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(PollingTimeoutSeconds));
            try
            {
                object @event = await consumer.GetNextEventAsync(cts.Token);
                events.Add((IEvent)@event);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }
        }

        await ApiUtils.WriteResponseAsJsonAsync(events.ToArray(), JsonContext.Default.IEventArray, context, cancellationToken);
    }

    private static bool ParseQueueIdParam(string query, out Guid? queueId)
    {
        queueId = null;

        foreach (var kv in new QueryStringEnumerable(query))
        {
            ReadOnlySpan<char> name = kv.DecodeName().Span;
            if (name.Equals("queueId", StringComparison.OrdinalIgnoreCase) && kv.EncodedValue.Length > 0)
            {
                if (!Guid.TryParse(kv.DecodeValue().Span, out Guid value))
                {
                    return false;
                }

                queueId = value;
            }
        }

        return true;
    }
}
