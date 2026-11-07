using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiangyeShiling.Networking;

/// <summary>
/// 有界且严格的请求格式。未知版本、额外/重复/缺失字段及客户端库存载荷一律拒绝，
/// 失败返回 null，不生成默认请求，不修改输入原文；传输端仍须在接收前限制自身消息大小。
/// </summary>
public static class MachineRequestCodec
{
    public const int MaxUtf8Bytes = 4096;
    public const int MaxNameLength = 128;
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "ProtocolVersion", "SessionId", "RequestId", "ActorId", "LocationName", "TileX", "TileY",
        "InstanceId", "ExpectedRevision", "Action", "RecipeId"
    };
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static string Write(MachineRequestEnvelope request)
    {
        if (!IsValid(request))
            throw new ArgumentException("Invalid machine request; refusing to serialize it.", nameof(request));
        string json = JsonSerializer.Serialize(request, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaxUtf8Bytes)
            throw new ArgumentException("Machine request exceeds the wire limit.", nameof(request));
        return json;
    }

    /// <summary>格式通过只说明意图可读取；主机权限、发送者身份与真实地图仍由 HostRequestGate 核验。</summary>
    public static bool TryRead(string? json, out MachineRequestEnvelope? request)
    {
        request = null;
        if (!IsBoundedWireText(json))
            return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 3 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
                if (!Fields.Contains(property.Name) || !seen.Add(property.Name))
                    return false;
            if (seen.Count != Fields.Count)
                return false;
            // JsonStringEnumConverter also accepts case-insensitive names; the wire contract does not.
            JsonElement action = root.GetProperty("Action");
            if (action.ValueKind != JsonValueKind.String || action.GetString() is not ("Start" or "Collect")
                || !CanonicalGuid(root.GetProperty("SessionId"))
                || !CanonicalGuid(root.GetProperty("RequestId"))
                || !CanonicalGuid(root.GetProperty("InstanceId")))
                return false;
            MachineRequestEnvelope? candidate = JsonSerializer.Deserialize<MachineRequestEnvelope>(json, Options);
            if (!IsValid(candidate))
                return false;
            request = candidate;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // BUG-2026-013: JsonDocument accepts escaped unpaired surrogates, but GetString/Name
            // throws while decoding them. The strict reader must reject this input without escaping its boundary.
            return false;
        }
    }

    /// <summary>也核对直接构造的内存请求，不能只保护 JSON 入口。无游戏数据写入或身份认证副作用。</summary>
    public static bool IsValid(MachineRequestEnvelope? request) => request is not null
        && request.ProtocolVersion == MachineRequestEnvelope.CurrentProtocol
        && request.SessionId != Guid.Empty && request.RequestId != Guid.Empty && request.ActorId != 0
        && ValidName(request.LocationName) && request.TileX >= 0 && request.TileY >= 0
        && request.InstanceId != Guid.Empty && request.ExpectedRevision >= 0
        && (request.Action == MachineRequestAction.Start ? ValidName(request.RecipeId)
            : request.Action == MachineRequestAction.Collect && request.RecipeId is null);

    internal static bool ValidName(string? name) => name is { Length: > 0 and <= MaxNameLength }
        && name == name.Trim() && !name.Any(char.IsControl) && ValidUtf16(name);

    // Shared by the read-only handshake codec; preserve BUG-013's literal UTF-16 check before conversion.
    internal static bool IsBoundedWireText([NotNullWhen(true)] string? text) => !string.IsNullOrWhiteSpace(text)
        && text.Length <= MaxUtf8Bytes && ValidUtf16(text) && Encoding.UTF8.GetByteCount(text) <= MaxUtf8Bytes;

    private static bool ValidUtf16(string text)
    {
        // Reject literal incomplete UTF-16 before JSON's UTF-8 conversion can replace it with U+FFFD.
        // Valid surrogate pairs remain allowed in names; never normalize the bound request's identity.
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (++i == text.Length || !char.IsLowSurrogate(text[i])) return false;
            }
            else if (char.IsLowSurrogate(text[i])) return false;
        }
        return true;
    }

    private static bool CanonicalGuid(JsonElement element) => element.ValueKind == JsonValueKind.String
        && Guid.TryParseExact(element.GetString(), "D", out _);
}
