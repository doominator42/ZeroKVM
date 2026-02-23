namespace ZeroKvm;

internal readonly struct FrameArea
{
    public ushort Width { get; init; }
    public ushort Height { get; init; }
    public ushort LineStride { get; init; }

    public ushort ModifiedX1 { get; init; }
    public ushort ModifiedY1 { get; init; }
    public ushort ModifiedX2 { get; init; }
    public ushort ModifiedY2 { get; init; }

    public bool WasModified => ModifiedY2 > ModifiedY1;
}
