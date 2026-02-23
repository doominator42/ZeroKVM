using System.Text.Json.Serialization;

namespace ZeroKvm.HttpApi;

[JsonSourceGenerationOptions(
    MaxDepth = 8,
    NumberHandling = JsonNumberHandling.Strict,
    PropertyNameCaseInsensitive = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(KeyboardEventRequest))]
[JsonSerializable(typeof(KeyboardLedsResponse))]
[JsonSerializable(typeof(PointerEventRequest))]
[JsonSerializable(typeof(UsbStateResponse))]
[JsonSerializable(typeof(QueueCreatedResponse))]
[JsonSerializable(typeof(IEvent[]))]
internal partial class JsonContext : JsonSerializerContext { }
