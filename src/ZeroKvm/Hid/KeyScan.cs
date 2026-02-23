namespace ZeroKvm.Hid;

internal readonly struct KeyScan
{
    private const uint IsDownMask = 0x10000U;

    public KeyScan(byte value, bool isDown, int delay = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)delay, byte.MaxValue);
        _value = value | ((uint)delay << 8) | (isDown ? IsDownMask : 0);
    }

    private readonly uint _value;

    public byte Value => (byte)_value;

    public int Delay => (byte)(_value >> 8);

    public bool IsDown => (_value & IsDownMask) != 0;

    public bool TryGetAsModifier(out HidKeyModifiers modifier)
    {
        modifier = Value switch
        {
            0xe0 => HidKeyModifiers.LeftControl,
            0xe1 => HidKeyModifiers.LeftShift,
            0xe2 => HidKeyModifiers.LeftAlt,
            0xe3 => HidKeyModifiers.LeftGui,
            0xe4 => HidKeyModifiers.RightControl,
            0xe5 => HidKeyModifiers.RightShift,
            0xe6 => HidKeyModifiers.RightAlt,
            0xe7 => HidKeyModifiers.RightGui,
            _ => HidKeyModifiers.None,
        };

        return modifier != HidKeyModifiers.None;
    }
}
