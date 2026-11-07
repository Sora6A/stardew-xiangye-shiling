using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GameHarness;
using NetworkLifecycleCheck;

internal static class NetworkLifecycleReaderScenarios
{
    public static void Run(Action<string, Action> test)
    {
        static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        var (host, client) = NetworkLifecycleScenarios.Fixture();
        var options = new JsonSerializerOptions(); options.Converters.Add(new JsonStringEnumConverter());
        byte[] Serialize(NetworkLifecycleReport value) => JsonSerializer.SerializeToUtf8Bytes(value, options);
        bool Refused(byte[] bytes)
        {
            try { using var stream = new MemoryStream(bytes); NetworkLifecycleReportReader.Read(stream); return false; }
            catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException or JsonException or KeyNotFoundException) { return true; }
        }
        test("lifecycle strict reader accepts serialized synthetic pair without changing raw bytes", () =>
        {
            var bytes = Serialize(host); var original = bytes.ToArray(); using var stream = new MemoryStream(bytes);
            var read = NetworkLifecycleReportReader.Read(stream);
            Assert(bytes.SequenceEqual(original) && read.Hash == Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), "Raw source changed/hash mismatch.");
            using var other = new MemoryStream(Serialize(client));
            Assert(NetworkLifecycleEvidence.PairMatches(read.Report, NetworkLifecycleReportReader.Read(other).Report), "Strict parsed pair failed.");
        });
        test("lifecycle strict reader refuses missing top-level and nested capability fields", () =>
        {
            foreach (string path in new[] { "Saved", "FullNet02Accepted", "ProbeSent", "Faulted", "WritesEnabled", "BodyReadAttempted" })
            {
                var node = JsonNode.Parse(Serialize(host))!.AsObject();
                if (path is "Saved" or "FullNet02Accepted" or "ProbeSent") node.Remove(path);
                else if (path == "BodyReadAttempted") node["InitialReady"]!["Protocol"]!["Trace"]![0]!["Observation"]!.AsObject().Remove(path);
                else node["InitialReady"]!["Protocol"]!.AsObject().Remove(path);
                Assert(Refused(Encoding.UTF8.GetBytes(node.ToJsonString())), "Missing declared field defaulted into acceptance.");
            }
        });
        test("lifecycle strict reader refuses duplicate alias and unknown declared members", () =>
        {
            string raw = Encoding.UTF8.GetString(Serialize(host));
            foreach (string bad in new[] { raw.Replace("\"Saved\":false", "\"Saved\":false,\"Saved\":true"),
                raw.Replace("\"Saved\":false", "\"Saved\":false,\"saved\":true"), raw.Replace("\"Saved\":false", "\"Saved\":false,\"Other\":0"),
                raw.Replace("\"Faulted\":false", "\"Faulted\":false,\"Faulted\":true") })
                Assert(Refused(Encoding.UTF8.GetBytes(bad)), "Ambiguous/unknown JSON member accepted.");
        });
        test("lifecycle strict reader refuses unbounded malformed null and wrong JSON types", () =>
        {
            Assert(Refused(new byte[NetworkLifecycleReportReader.MaximumBytes + 1]), "Oversize read accepted.");
            foreach (string raw in new[] { "null", "[]", "{", Encoding.UTF8.GetString(Serialize(host)).Replace("\"Saved\":false", "\"Saved\":0") })
                Assert(Refused(Encoding.UTF8.GetBytes(raw)), "Malformed/wrong typed JSON accepted.");
        });
    }
}
