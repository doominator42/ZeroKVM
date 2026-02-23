using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroKvm.Hid;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct HidBootKeyboardReport : IHidReport, IEquatable<HidBootKeyboardReport>
{
    public readonly HidKeyModifiers Modifiers;
    private readonly byte _reserved;
    private readonly byte _keyPress0;
    private readonly byte _keyPress1;
    private readonly byte _keyPress2;
    private readonly byte _keyPress3;
    private readonly byte _keyPress4;
    private readonly byte _keyPress5;

    public int KeyPressCount =>
        _keyPress0 == 0 ? 0 :
        _keyPress1 == 0 ? 1 :
        _keyPress2 == 0 ? 2 :
        _keyPress3 == 0 ? 3 :
        _keyPress4 == 0 ? 4 :
        _keyPress5 == 0 ? 5 : 6;

    public HidBootKeyboardReport AddModifiers(HidKeyModifiers modifiers)
    {
        HidBootKeyboardReport copy = this;
        Span<byte> bytes = MemoryMarshal.AsBytes(new Span<HidBootKeyboardReport>(ref copy));
        bytes[0] |= (byte)modifiers;
        return copy;
    }

    public HidBootKeyboardReport RemoveModifiers(HidKeyModifiers modifiers)
    {
        HidBootKeyboardReport copy = this;
        Span<byte> bytes = MemoryMarshal.AsBytes(new Span<HidBootKeyboardReport>(ref copy));
        bytes[0] &= (byte)~modifiers;
        return copy;
    }

    public HidBootKeyboardReport AddKeyPress(byte key)
    {
        HidBootKeyboardReport copy = this;
        Span<byte> bytes = MemoryMarshal.AsBytes(new Span<HidBootKeyboardReport>(ref copy));
        for (int i = 2; i < 8; i++)
        {
            if (bytes[i] == key)
            {
                return this;
            }
            else if (bytes[i] == 0)
            {
                bytes[i] = key;
                return copy;
            }
        }

        throw new ArgumentException("No key press slot available");
    }

    public HidBootKeyboardReport RemoveKeyPress(byte key)
    {
        HidBootKeyboardReport copy = this;
        Span<byte> bytes = MemoryMarshal.AsBytes(new Span<HidBootKeyboardReport>(ref copy));
        for (int i = 2; i < 8; i++)
        {
            byte keyPress = bytes[i];
            if (keyPress == 0)
            {
                break;
            }
            else if (keyPress == key)
            {
                for (int j = i + 1; j < 8; j++)
                {
                    bytes[j - 1] = bytes[j];
                }

                bytes[7] = 0;
                break;
            }
        }

        return copy;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is HidBootKeyboardReport other && Equals(other);
    }

    public bool Equals(HidBootKeyboardReport other)
    {
        return this == other;
    }

    public override int GetHashCode()
    {
        return Unsafe.BitCast<HidBootKeyboardReport, ulong>(this).GetHashCode();
    }

    public override string ToString()
    {
        return $"{_keyPress0:x2} {_keyPress1:x2} {_keyPress2:x2} {_keyPress3:x2} {_keyPress4:x2} {_keyPress5:x2} {Modifiers}";
    }

    public int WriteTo(ReadOnlySpan<byte> bytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytes.Length, Unsafe.SizeOf<HidBootKeyboardReport>());
        Unsafe.As<byte, HidBootKeyboardReport>(ref MemoryMarshal.GetReference(bytes)) = this;
        return Unsafe.SizeOf<HidBootKeyboardReport>();
    }

    public static bool operator ==(HidBootKeyboardReport left, HidBootKeyboardReport right)
    {
        return Unsafe.BitCast<HidBootKeyboardReport, ulong>(left) == Unsafe.BitCast<HidBootKeyboardReport, ulong>(right);
    }

    public static bool operator !=(HidBootKeyboardReport left, HidBootKeyboardReport right)
    {
        return Unsafe.BitCast<HidBootKeyboardReport, ulong>(left) != Unsafe.BitCast<HidBootKeyboardReport, ulong>(right);
    }

    public static int ReportLength => Unsafe.SizeOf<HidBootKeyboardReport>();

    public static bool IsBootDescriptor => true;

    public static ReadOnlySpan<byte> Descriptor => [
        0x05, 0x01, // Usage Page (Generic Desktop Ctrls)
        0x09, 0x06, // Usage (Keyboard)
        0xA1, 0x01, // Collection (Application)
        0x05, 0x07, //   Usage Page (Kbrd/Keypad)
        0x19, 0xE0, //   Usage Minimum (0xE0)
        0x29, 0xE7, //   Usage Maximum (0xE7)
        0x15, 0x00, //   Logical Minimum (0)
        0x25, 0x01, //   Logical Maximum (1)
        0x75, 0x01, //   Report Size (1)
        0x95, 0x08, //   Report Count (8)
        0x81, 0x02, //   Input (Data,Var,Abs,No Wrap,Linear,Preferred State,No Null Position)
        0x95, 0x01, //   Report Count (1)
        0x75, 0x08, //   Report Size (8)
        0x81, 0x03, //   Input (Const,Var,Abs,No Wrap,Linear,Preferred State,No Null Position)
        0x95, 0x05, //   Report Count (5)
        0x75, 0x01, //   Report Size (1)
        0x05, 0x08, //   Usage Page (LEDs)
        0x19, 0x01, //   Usage Minimum (Num Lock)
        0x29, 0x05, //   Usage Maximum (Kana)
        0x91, 0x02, //   Output (Data,Var,Abs,No Wrap,Linear,Preferred State,No Null Position,Non-volatile)
        0x95, 0x01, //   Report Count (1)
        0x75, 0x03, //   Report Size (3)
        0x91, 0x03, //   Output (Const,Var,Abs,No Wrap,Linear,Preferred State,No Null Position,Non-volatile)
        0x95, 0x06, //   Report Count (6)
        0x75, 0x08, //   Report Size (8)
        0x15, 0x00, //   Logical Minimum (0)
        0x25, 0x65, //   Logical Maximum (101)
        0x05, 0x07, //   Usage Page (Kbrd/Keypad)
        0x19, 0x00, //   Usage Minimum (0x00)
        0x29, 0x65, //   Usage Maximum (0x65)
        0x81, 0x00, //   Input (Data,Array,Abs,No Wrap,Linear,Preferred State,No Null Position)
        0xC0,       // End Collection
    ];
}
