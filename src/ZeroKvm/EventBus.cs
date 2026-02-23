using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace ZeroKvm;

internal class EventBus
{
    private const int MaxQueuedEvents = 1000;

    private readonly ConcurrentDictionary<Guid, Consumer> _consumers = new();

    public Consumer CreateConsumer()
    {
        Consumer consumer = new(this);
        _consumers.TryAdd(consumer.Id, consumer);
        return consumer;
    }

    public Consumer? GetConsumer(Guid id)
    {
        return _consumers.GetValueOrDefault(id);
    }

    public void Publish(object eventData)
    {
        long timestamp = Stopwatch.GetTimestamp();
        foreach (Consumer consumer in _consumers.Values)
        {
            if (consumer.GetLastReadElapsed(timestamp) > TimeSpan.FromSeconds(60))
            {
                consumer.Dispose();
            }
            else
            {
                consumer.Enqueue(eventData);
            }
        }
    }

    public class Consumer : IDisposable
    {
        private static readonly BoundedChannelOptions _channelOptions = new(MaxQueuedEvents)
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false,
        };

        public Consumer(EventBus bus)
        {
            ArgumentNullException.ThrowIfNull(bus);
            _bus = bus;
            _channel = Channel.CreateBounded<object>(_channelOptions);
            _lastRead = Stopwatch.GetTimestamp();
            Id = Guid.NewGuid();
        }

        private readonly EventBus _bus;
        private readonly Channel<object> _channel;
        private long _lastRead;
        private int _readingCount;
        private int _disposed;

        public Guid Id { get; }

        public TimeSpan GetLastReadElapsed(long currentTimestamp)
        {
            return Stopwatch.GetElapsedTime(_lastRead, currentTimestamp);
        }

        public void Enqueue(object @event)
        {
            if (!_channel.Writer.TryWrite(@event))
            {
                Dispose();
            }
        }

        public ValueTask<object> GetNextEventAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _lastRead = Stopwatch.GetTimestamp();
            ValueTask<object> readTask = _channel.Reader.ReadAsync(cancellationToken);
            return readTask.IsCompleted ? readTask : WaitForReadAsync(this, readTask);

            static async ValueTask<object> WaitForReadAsync(Consumer @this, ValueTask<object> task)
            {
                Interlocked.Increment(ref @this._readingCount);
                try
                {
                    return await task;
                }
                finally
                {
                    @this._lastRead = Stopwatch.GetTimestamp();
                    Interlocked.Decrement(ref @this._readingCount);
                }
            }
        }

        public bool TryGetNextEvent([MaybeNullWhen(false)] out object @event)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _lastRead = Stopwatch.GetTimestamp();
            return _channel.Reader.TryRead(out @event);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _bus._consumers.TryRemove(Id, out _);
                _channel.Writer.Complete();
            }
        }
    }
}
