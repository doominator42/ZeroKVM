namespace ZeroKvm.HttpApi;

internal class KeyboardEventRequest : IRequestValidation
{
    public required KeyEvent[] Keys { get; init; }

    public bool Reset { get; init; }

    public string? Validate()
    {
        if (Keys is null || Keys.Length == 0)
        {
            return "Keys cannot be empty";
        }

        foreach (KeyEvent? @event in Keys)
        {
            if (@event is null)
            {
                return "Key cannot be null";
            }
            else if (@event.Validate() is { } error)
            {
                return error;
            }
        }

        return null;
    }

    public class KeyEvent : IRequestValidation
    {
        public required byte ScanCode { get; init; }

        public required bool IsDown { get; init; }

        public byte Delay { get; init; }

        public string? Validate()
        {
            if (ScanCode == 0)
            {
                return "Invalid scan code";
            }

            return null;
        }
    }
}
