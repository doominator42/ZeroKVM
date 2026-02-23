namespace ZeroKvm.HttpApi;

internal class PointerEventRequest : IRequestValidation
{
    public required PointerType Type { get; init; }

    public required Event[] Events { get; init; }

    public bool Reset { get; init; }

    public string? Validate()
    {
        if (Events is null || Events.Length == 0)
        {
            return "Events cannot be empty";
        }

        foreach (Event? @event in Events)
        {
            if (@event is null)
            {
                return "Event cannot be null";
            }
            else if (@event.Validate() is { } error)
            {
                return error;
            }
        }

        return null;
    }

    public class Event : IRequestValidation
    {
        public bool? Left { get; init; }

        public bool? Middle { get; init; }

        public bool? Right { get; init; }

        public short? X { get; init; }

        public short? Y { get; init; }

        public sbyte? Wheel { get; init; }

        public byte Delay { get; init; }

        public string? Validate()
        {
            if ((X is null) != (Y is null))
            {
                return "Both X and Y must have a value";
            }

            return null;
        }
    }
}
