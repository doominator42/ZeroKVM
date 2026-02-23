using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroKvm.Hid;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct HidWheelReport : IHidReport, IEquatable<HidWheelReport>
{
    public HidWheelReport(sbyte y)
        : this(2, y)
    { }

    public HidWheelReport(byte reportId, sbyte y)
    {
        _reportId = reportId;
        Y = y;
    }

    private readonly byte _reportId;
    public readonly sbyte Y;

    public HidWheelReport AddY(sbyte y)
    {
        return new((sbyte)Math.Clamp(Y + y, sbyte.MinValue, sbyte.MaxValue));
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is HidWheelReport other && Equals(other);
    }

    public bool Equals(HidWheelReport other)
    {
        return this == other;
    }

    public override int GetHashCode()
    {
        return Unsafe.BitCast<HidWheelReport, ushort>(this).GetHashCode();
    }

    public override string ToString()
    {
        return Y.ToString();
    }

    public int WriteTo(ReadOnlySpan<byte> bytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytes.Length, Unsafe.SizeOf<HidWheelReport>());
        Unsafe.As<byte, HidWheelReport>(ref MemoryMarshal.GetReference(bytes)) = this;
        return Unsafe.SizeOf<HidWheelReport>();
    }

    public static bool operator ==(HidWheelReport left, HidWheelReport right)
    {
        return Unsafe.BitCast<HidWheelReport, ushort>(left) == Unsafe.BitCast<HidWheelReport, ushort>(right);
    }

    public static bool operator !=(HidWheelReport left, HidWheelReport right)
    {
        return Unsafe.BitCast<HidWheelReport, ushort>(left) != Unsafe.BitCast<HidWheelReport, ushort>(right);
    }

    public static int ReportLength => Unsafe.SizeOf<HidWheelReport>();

    public static bool IsBootDescriptor => false;

    public static ReadOnlySpan<byte> Descriptor => [
        0x85, 0x01, //   Report ID (1)
        0x09, 0x38, //   Usage (Wheel)
        0x15, 0x81, //   Logical Minimum (-127)
        0x25, 0x7F, //   Logical Maximum (127)
        0x35, 0x00, //   Physical Minimum (0)
        0x45, 0x00, //   Physical Maximum (0)
        0x75, 0x08, //   Report Size (8)
        0x95, 0x01, //   Report Count (1)
        0x81, 0x06, //   Input (Data,Var,Rel,No Wrap,Linear,Preferred State,No Null Position)
    ];
}
