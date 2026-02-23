using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroKvm.Hid;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct HidAbsoluteMouseReport : IHidReport, IEquatable<HidAbsoluteMouseReport>
{
    public HidAbsoluteMouseReport(HidMouseButtons buttons, short x, short y)
        : this(1, buttons, x, y)
    { }

    public HidAbsoluteMouseReport(byte reportId, HidMouseButtons buttons, short x, short y)
    {
        _reportId = reportId;
        Buttons = buttons;
        X = x;
        Y = y;
    }

    private readonly byte _reportId;
    public readonly HidMouseButtons Buttons;
    public readonly short X;
    public readonly short Y;

    public HidAbsoluteMouseReport AddButtons(HidMouseButtons buttons)
    {
        return new(Buttons | buttons, X, Y);
    }

    public HidAbsoluteMouseReport RemoveButtons(HidMouseButtons buttons)
    {
        return new((HidMouseButtons)((byte)Buttons & (byte)~buttons), X, Y);
    }

    public HidAbsoluteMouseReport RemoveAllButtons()
    {
        return new(HidMouseButtons.None, X, Y);
    }

    public HidAbsoluteMouseReport WithPosition(short x, short y)
    {
        return new(Buttons, x, y);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is HidAbsoluteMouseReport other && Equals(other);
    }

    public bool Equals(HidAbsoluteMouseReport other)
    {
        return this == other;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reportId, Buttons, X, Y);
    }

    public override string ToString()
    {
        return $"{X} {Y} {Buttons}";
    }

    public int WriteTo(ReadOnlySpan<byte> bytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytes.Length, Unsafe.SizeOf<HidAbsoluteMouseReport>());
        Unsafe.As<byte, HidAbsoluteMouseReport>(ref MemoryMarshal.GetReference(bytes)) = this;
        return Unsafe.SizeOf<HidAbsoluteMouseReport>();
    }

    public static bool operator ==(HidAbsoluteMouseReport left, HidAbsoluteMouseReport right)
    {
        return left._reportId == right._reportId &&
            left.Buttons == right.Buttons &&
            left.X == right.X &&
            left.Y == right.Y;
    }

    public static bool operator !=(HidAbsoluteMouseReport left, HidAbsoluteMouseReport right)
    {
        return left._reportId != right._reportId ||
            left.Buttons != right.Buttons ||
            left.X != right.X ||
            left.Y != right.Y;
    }

    public static int ReportLength => Unsafe.SizeOf<HidAbsoluteMouseReport>();

    public static bool IsBootDescriptor => false;

    public static ReadOnlySpan<byte> Descriptor => [
        0x85, 0x01,         //   Report ID (1)
        0x09, 0x01,         //   Usage (Pointer)
        0xA1, 0x00,         //   Collection (Physical)
        0x05, 0x09,         //     Usage Page (Button)
        0x19, 0x01,         //     Usage Minimum (0x01)
        0x29, 0x03,         //     Usage Maximum (0x03)
        0x15, 0x00,         //     Logical Minimum (0)
        0x25, 0x01,         //     Logical Maximum (1)
        0x75, 0x01,         //     Report Size (1)
        0x95, 0x03,         //     Report Count (3)
        0x81, 0x02,         //     Input (Data,Var,Abs,No Wrap,Linear,Preferred State,No Null Position)
        0x95, 0x01,         //     Report Count (1)
        0x75, 0x05,         //     Report Size (5)
        0x81, 0x03,         //     Input (Const,Var,Abs,No Wrap,Linear,Preferred State,No Null Position)
        0x05, 0x01,         //     Usage Page (Generic Desktop Ctrls)
        0x09, 0x30,         //     Usage (X)
        0x09, 0x31,         //     Usage (Y)
        0x16, 0x00, 0x00,   //     Logical Minimum (0)
        0x26, 0xFF, 0x7F,   //     Logical Maximum (32767)
        0x36, 0x00, 0x00,   //     Physical Minimum (0)
        0x46, 0xFF, 0x7F,   //     Physical Maximum (32767)
        0x75, 0x10,         //     Report Size (16)
        0x95, 0x02,         //     Report Count (2)
        0x81, 0x02,         //     Input (Data,Var,Abs,No Wrap,Linear,Preferred State,No Null Position)
        0xC0,               //   End Collection
    ];
}
