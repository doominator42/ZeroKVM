namespace ZeroKvm.HttpApi;

internal class KeyboardLedsResponse
{
    public required bool NumLock { get; init; }
    public required bool CapsLock { get; init; }
    public required bool ScrollLock { get; init; }
    public required bool Compose { get; init; }
    public required bool Kana { get; init; }
}
