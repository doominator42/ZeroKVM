using System.Runtime.InteropServices;

namespace ZeroKvm.Hid;

[StructLayout(LayoutKind.Auto)]
internal readonly struct PointerEvent
{
    public PointerEvent(PointerButtons downButtons, PointerButtons upButtons, (short X, short Y)? move, sbyte wheel, int delay = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)delay, byte.MaxValue);
        DownButtons = downButtons;
        UpButtons = upButtons;
        Move = move;
        Wheel = wheel;
        Delay = delay;
    }

    public PointerButtons DownButtons { get; }

    public PointerButtons UpButtons { get; }

    public (short X, short Y)? Move { get; }

    public sbyte Wheel { get; }

    public int Delay { get; }
}
