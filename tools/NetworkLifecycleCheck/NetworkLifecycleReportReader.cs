using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameHarness;
using XiangyeShiling.Networking;

namespace NetworkLifecycleCheck;

/// <summary>
/// 有界严格读取schema1原报告；所有声明必须存在，未知/重复/大小写别名字段不获默认能力。
/// 仅返回解析对象与原字节SHA，不修改输入，是否真实来自SDK由独立部署/日志/存档审计证明。
/// </summary>
public static class NetworkLifecycleReportReader
{
    public const int MaximumBytes = 2 * 1024 * 1024;
    public static (NetworkLifecycleReport Report, string Hash) Read(Stream source)
    {
        byte[] bytes = new byte[MaximumBytes + 1];
        int length = 0, count;
        while (length < bytes.Length && (count = source.Read(bytes, length, bytes.Length - length)) != 0) length += count;
        if (length == bytes.Length) throw new InvalidDataException("Oversize report.");
        var raw = bytes.AsSpan(0, length).ToArray();
        using var document = JsonDocument.Parse(raw);
        static void Fields<T>(JsonElement value)
        {
            var names = typeof(T).GetProperties().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
                if (!names.Contains(property.Name) || !seen.Add(property.Name)) throw new InvalidDataException("Unknown or duplicate field.");
            if (!seen.SetEquals(names)) throw new InvalidDataException("Missing declared field.");
        }
        var root = document.RootElement;
        Fields<NetworkLifecycleReport>(root);
        foreach (string name in new[] { "InitialReady", "DisconnectOrTitle", "Reconnected", "ClosingTitle" })
        {
            var point = root.GetProperty(name);
            if (point.ValueKind == JsonValueKind.Null) continue;
            Fields<LifecycleCheckpoint>(point);
            var protocol = point.GetProperty("Protocol");
            Fields<ProtocolDiagnosticSnapshot>(protocol);
            foreach (var peer in protocol.GetProperty("Peers").EnumerateArray()) Fields<ProtocolPeerStatus>(peer);
            foreach (var trace in protocol.GetProperty("Trace").EnumerateArray())
            {
                Fields<ProtocolTrace>(trace);
                var observation = trace.GetProperty("Observation");
                if (observation.ValueKind != JsonValueKind.Null) Fields<ProtocolObservation>(observation);
                var quota = trace.GetProperty("Quota");
                if (quota.ValueKind != JsonValueKind.Null) Fields<ProtocolQuotaWindow>(quota);
            }
        }
        var options = new JsonSerializerOptions(); options.Converters.Add(new JsonStringEnumConverter());
        var report = JsonSerializer.Deserialize<NetworkLifecycleReport>(raw, options) ?? throw new InvalidDataException("Null report.");
        return (report, Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant());
    }
}
