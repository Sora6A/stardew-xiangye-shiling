using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameHarness;
using NetworkLifecycleCheck;
using XiangyeShiling.Networking;

namespace NetworkWelcomeReplayCheck;

/// <summary>只读2MiB有界报告；完整字段/重复/别名检查覆盖嵌套原生命周期及前后快照，不补默认能力。</summary>
public static class NetworkWelcomeReplayReportReader
{
    public const int MaximumBytes = NetworkLifecycleReportReader.MaximumBytes;
    public static (NetworkWelcomeReplayReport Report, string Hash) Read(Stream source)
    {
        byte[] buffer = new byte[MaximumBytes + 1]; int length = 0, count;
        while (length < buffer.Length && (count = source.Read(buffer, length, buffer.Length - length)) != 0) length += count;
        if (length == buffer.Length) throw new InvalidDataException("Oversize replay report.");
        byte[] raw = buffer.AsSpan(0, length).ToArray();
        using var document = JsonDocument.Parse(raw);
        static void Fields<T>(JsonElement value)
        {
            var names = typeof(T).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
                if (!names.Contains(property.Name) || !seen.Add(property.Name)) throw new InvalidDataException("Unknown or duplicate field.");
            if (!seen.SetEquals(names)) throw new InvalidDataException("Missing field.");
        }
        var root = document.RootElement;
        Fields<NetworkWelcomeReplayReport>(root);
        using (var nested = new MemoryStream(Encoding.UTF8.GetBytes(root.GetProperty("Lifecycle").GetRawText())))
            NetworkLifecycleReportReader.Read(nested);
        foreach (string name in new[] { "BeforeReplay", "AfterReplay" })
        {
            var point = root.GetProperty(name);
            if (point.ValueKind == JsonValueKind.Null) continue;
            Fields<LifecycleCheckpoint>(point);
            var protocol = point.GetProperty("Protocol"); Fields<ProtocolDiagnosticSnapshot>(protocol);
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
        var report = JsonSerializer.Deserialize<NetworkWelcomeReplayReport>(raw, options) ?? throw new InvalidDataException("Null report.");
        return (report, Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant());
    }
}
