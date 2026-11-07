using System.Text.Json;
using GameHarness;
using NetworkWelcomeReplayCheck;

// No output-file argument: rejected inputs never produce a success artifact or modify source reports.
try
{
    if (args.Length != 2) throw new ArgumentException("Expected host and client original replay reports.");
    using var h = File.OpenRead(args[0]); using var c = File.OpenRead(args[1]);
    var host = NetworkWelcomeReplayReportReader.Read(h); var client = NetworkWelcomeReplayReportReader.Read(c);
    if (!NetworkWelcomeReplayEvidence.PairMatches(host.Report, client.Report)) throw new InvalidDataException("Inconsistent replay evidence.");
    Console.WriteLine(JsonSerializer.Serialize(new { SchemaVersion = 1, OldWelcomeReplayRejected = true,
        SameIdRehandshakeObserved = true, HostWorldSessionPreserved = true, FullNet02Accepted = false,
        OldPacketReplayVerified = false, LoadedQueueLifecycleVerified = false, host.Report.Lifecycle.ExpectedFixtureId,
        HostReportSha256 = host.Hash, ClientReportSha256 = client.Hash }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException
    or JsonException or KeyNotFoundException or InvalidOperationException or NullReferenceException)
{
    Console.Error.WriteLine("REJECT: incomplete or inconsistent old Welcome replay evidence; inspect original reports and contract.");
    return 1;
}
