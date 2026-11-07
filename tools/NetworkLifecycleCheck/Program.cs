using System.Text.Json;
using GameHarness;
using NetworkLifecycleCheck;

// Only bounded read-only files and stdout. Never starts the game or rewrites reports/saves/configs.
try
{
    if (args.Length != 2) throw new ArgumentException("Expected host and client report paths.");
    using var hostFile = File.OpenRead(args[0]); using var clientFile = File.OpenRead(args[1]);
    var host = NetworkLifecycleReportReader.Read(hostFile);
    var client = NetworkLifecycleReportReader.Read(clientFile);
    if (!NetworkLifecycleEvidence.PairMatches(host.Report, client.Report)) throw new InvalidDataException("Inconsistent evidence.");
    Console.WriteLine(JsonSerializer.Serialize(new { SchemaVersion = 1, SameIdRehandshakeObserved = true,
        HostWorldSessionPreserved = true, FullNet02Accepted = false, OldPacketReplayVerified = false,
        LoadedQueueLifecycleVerified = false, host.Report.ExpectedFixtureId,
        HostReportSha256 = host.Hash, ClientReportSha256 = client.Hash }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or JsonException
    or KeyNotFoundException or InvalidOperationException or NullReferenceException)
{
    Console.Error.WriteLine("REJECT: incomplete or inconsistent lifecycle evidence; inspect the archived reports and contract.");
    return 1;
}
