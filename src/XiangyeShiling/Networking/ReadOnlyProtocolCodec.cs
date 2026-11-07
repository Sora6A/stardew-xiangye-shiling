using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiangyeShiling.Networking;

/// <summary>仅传输诊断握手/拒绝，不携带库存、时间、设备快照或成功回执。字段名区分大小写。</summary>
public sealed record ProtocolHello(int ProtocolVersion, Guid Challenge, long ActorId);
public sealed record ProtocolWelcome(int ProtocolVersion, int RequestProtocolVersion, Guid Challenge,
    Guid SessionId, long HostPlayerId, bool WritesEnabled);
public enum ProtocolRejection { WritesDisabled, HandshakeRequired, WrongSession, ActorMismatch, InvalidRequest }
public sealed record ProtocolRejected(int ProtocolVersion, Guid SessionId, Guid? RequestId,
    long ActorId, ProtocolRejection Reason);
public sealed record OutboundProtocolMessage(long PlayerId, string MessageType, string Json);

/// <summary>
/// P1-NET-02 的独立版本与严格 JSON 格式。外层 SMAPI 载荷必须是字符串，以保留内部重复字段证据。
/// 所有 Welcome 固定 WritesEnabled=false；尝试开启写入的响应属于无效协议，而非配置开关。
/// </summary>
public static class ReadOnlyProtocolCodec
{
    public const int CurrentProtocol = 1;
    public const string HelloType = "XYS/Hello/1";
    public const string WelcomeType = "XYS/Welcome/1";
    public const string RequestType = "XYS/Request/1";
    public const string RejectedType = "XYS/Rejected/1";
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static string Write(ProtocolHello value) => Serialize(value, Valid(value));
    public static string Write(ProtocolWelcome value) => Serialize(value, Valid(value));
    public static string Write(ProtocolRejected value) => Serialize(value, Valid(value));
    public static bool TryReadHello(string? json, out ProtocolHello? value) => Read(json,
        new[] { "ProtocolVersion", "Challenge", "ActorId" }, Valid, out value);
    public static bool TryReadWelcome(string? json, out ProtocolWelcome? value) => Read(json,
        new[] { "ProtocolVersion", "RequestProtocolVersion", "Challenge", "SessionId", "HostPlayerId", "WritesEnabled" }, Valid, out value);
    public static bool TryReadRejected(string? json, out ProtocolRejected? value) => Read(json,
        new[] { "ProtocolVersion", "SessionId", "RequestId", "ActorId", "Reason" }, Valid, out value);

    private static bool Valid(ProtocolHello? value) => value is not null
        && value.ProtocolVersion == CurrentProtocol && value.Challenge != Guid.Empty && value.ActorId != 0;
    private static bool Valid(ProtocolWelcome? value) => value is not null
        && value.ProtocolVersion == CurrentProtocol && value.RequestProtocolVersion == MachineRequestEnvelope.CurrentProtocol
        && value.Challenge != Guid.Empty && value.SessionId != Guid.Empty && value.HostPlayerId != 0 && !value.WritesEnabled;
    private static bool Valid(ProtocolRejected? value) => value is not null
        && value.ProtocolVersion == CurrentProtocol && value.SessionId != Guid.Empty && value.ActorId != 0
        && value.RequestId != Guid.Empty && Enum.IsDefined(typeof(ProtocolRejection), value.Reason);

    private static string Serialize<T>(T value, bool valid)
    {
        if (!valid) throw new ArgumentException("Invalid read-only protocol message.", nameof(value));
        return JsonSerializer.Serialize(value, Options);
    }

    private static bool Read<T>(string? json, string[] fields, Func<T?, bool> validate, out T? value) where T : class
    {
        value = null;
        if (!MachineRequestCodec.IsBoundedWireText(json)) return false;
        try
        {
            using var document = JsonDocument.Parse(json!, new JsonDocumentOptions { MaxDepth = 3 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!fields.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name)) return false;
                if (property.Name is "Challenge" or "SessionId" or "RequestId")
                {
                    if (property.Name == "RequestId" && property.Value.ValueKind == JsonValueKind.Null) continue;
                    if (property.Value.ValueKind != JsonValueKind.String
                        || !Guid.TryParseExact(property.Value.GetString(), "D", out _)) return false;
                }
                if (property.Name == "Reason" && (property.Value.ValueKind != JsonValueKind.String
                    || !Enum.GetNames<ProtocolRejection>().Contains(property.Value.GetString(), StringComparer.Ordinal))) return false;
            }
            if (seen.Count != fields.Length) return false;
            T? candidate = JsonSerializer.Deserialize<T>(json!, Options);
            if (!validate(candidate)) return false;
            value = candidate;
            return true;
        }
        catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; } // BUG-013 escaped incomplete Unicode must also fail closed here.
    }
}
