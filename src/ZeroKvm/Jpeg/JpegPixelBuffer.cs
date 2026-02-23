namespace ZeroKvm.Jpeg;

internal readonly ref struct JpegPixelBuffer
{
    public required ReadOnlySpan<byte> Buffer { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int BytesPerLine { get; init; }
    public required JpegPixelFormat Format { get; init; }

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Height);
        ArgumentOutOfRangeException.ThrowIfLessThan(BytesPerLine, Width);
        ArgumentOutOfRangeException.ThrowIfLessThan(Buffer.Length, (BytesPerLine * (Height - 1)) + Width);
    }
}
