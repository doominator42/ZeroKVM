using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroKvm.Hid;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct HidBootMouseReport : IHidReport, IEquatable<HidBootMouseReport>
{
    public HidBootMouseReport(HidMouseButtons buttons, sbyte x, sbyte y, sbyte wheel)
    {
        Buttons = buttons;
        X = x;
        Y = y;
        Wheel = wheel;
    }

    public readonly HidMouseButtons Buttons;
    public readonly sbyte X;
    public readonly sbyte Y;
    public readonly sbyte Wheel;

    public HidBootMouseReport RemoveAllButtons()
    {
        return new(HidMouseButtons.None, X, Y, Wheel);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is HidBootMouseReport reportObj && Equals(reportObj);
    }

    public bool Equals(HidBootMouseReport other)
    {
        return this == other;
    }

    public override int GetHashCode()
    {
        return (int)Unsafe.BitCast<HidBootMouseReport, uint>(this);
    }

    public override string ToString()
    {
        return $"{Buttons} {X} {Y} {Wheel}";
    }

    public static bool operator ==(HidBootMouseReport left, HidBootMouseReport right)
    {
        return Unsafe.BitCast<HidBootMouseReport, uint>(left) == Unsafe.BitCast<HidBootMouseReport, uint>(right);
    }

    public static bool operator !=(HidBootMouseReport left, HidBootMouseReport right)
    {
        return Unsafe.BitCast<HidBootMouseReport, uint>(left) != Unsafe.BitCast<HidBootMouseReport, uint>(right);
    }

    public int WriteTo(ReadOnlySpan<byte> bytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytes.Length, Unsafe.SizeOf<HidBootMouseReport>());
        Unsafe.As<byte, HidBootMouseReport>(ref MemoryMarshal.GetReference(bytes)) = this;
        return Unsafe.SizeOf<HidBootMouseReport>();
    }

    public static int ReportLength => Unsafe.SizeOf<HidBootMouseReport>();

    public static bool IsBootDescriptor => true;

    public static ReadOnlySpan<byte> Descriptor => [
        0x05, 0x01, // Usage Page (Generic Desktop Ctrls)
        0x09, 0x02, // Usage (Mouse)
        0xA1, 0x01, // Collection (Application)
        0x09, 0x01, //   Usage (Pointer)
        0xA1, 0x00, //   Collection (Physical)
        0x05, 0x09, //     Usage Page (Button)
        0x19, 0x01, //     Usage Minimum (0x01)
        0x29, 0x08, //     Usage Maximum (0x08)
        0x15, 0x00, //     Logical Minimum (0)
        0x25, 0x01, //     Logical Maximum (1)
        0x95, 0x08, //     Report Count (8)
        0x75, 0x01, //     Report Size (1)
        0x81, 0x02, //     Input (Data,Var,Abs,No Wrap,Linear,Preferred State,No Null Position)
        0x05, 0x01, //     Usage Page (Generic Desktop Ctrls)
        0x09, 0x30, //     Usage (X)
        0x09, 0x31, //     Usage (Y)
        0x09, 0x38, //     Usage (Wheel)
        0x15, 0x81, //     Logical Minimum (-127)
        0x25, 0x7F, //     Logical Maximum (127)
        0x75, 0x08, //     Report Size (8)
        0x95, 0x03, //     Report Count (3)
        0x81, 0x06, //     Input (Data,Var,Rel,No Wrap,Linear,Preferred State,No Null Position)
        0xC0,       //   End Collection
        0xC0,       // End Collection
    ];
}
