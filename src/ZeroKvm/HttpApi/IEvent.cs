using System.Text.Json.Serialization;

namespace ZeroKvm.HttpApi;

[JsonDerivedType(typeof(UsbStateEvent), "usb/state")]
[JsonDerivedType(typeof(KeyboardLedsEvent), "keyboard/leds")]
internal interface IEvent
{
}
