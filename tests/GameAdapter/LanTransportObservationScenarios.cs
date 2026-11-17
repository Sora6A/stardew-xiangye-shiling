using System.Reflection;
using System.Security.Cryptography;
using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.GameIntegration;

internal static class LanTransportObservationScenarios
{
    internal static void Run(Action<string, Action> test)
    {
        Type adapter = typeof(MachineService).Assembly.GetType(
            "XiangyeShiling.GameIntegration.LanTransportActorObservation", throwOnError: true)!;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var classify = adapter.GetMethod("Classify", flags) ?? throw new MissingMethodException(adapter.FullName, "Classify");
        const long actor = -51, other = 37, world = 2, connection = 3;

        string Observe(long? actualActor, long claim, long currentWorld, long currentConnection) =>
            classify.Invoke(null, new object?[] { actualActor, claim, world, connection, currentWorld, currentConnection })!.ToString()!;

        test("LAN observation rejects missing scope and mismatched actor metadata", () =>
        {
            Require(Observe(null, actor, world, connection) == "MissingScope", "No native scope cannot prove an actor.");
            Require(Observe(0, actor, world, connection) == "MissingScope", "Zero is not a native actor.");
            Require(Observe(actor, other, world, connection) == "ActorMismatch", "Message metadata cannot change the bound actor.");
            Require(Observe(actor, actor, world, connection) == "Match", "The native signed Farmer ID should match its own claim.");
        });

        test("LAN observation rejects stale world and disconnect generations", () =>
        {
            Require(Observe(actor, actor, world + 1, connection) == "Stale", "A new world invalidates prior observations.");
            Require(Observe(actor, actor, world, connection + 1) == "Stale", "A disconnect invalidates prior observations even with the same Farmer ID.");
        });

        test("LAN native callback binding resolves only for the pinned installed SDK pair", () =>
        {
            var find = adapter.GetMethod("FindBinding", flags) ?? throw new MissingMethodException(adapter.FullName, "FindBinding");
            bool expected = OperatingSystem.IsWindows()
                && typeof(Game1).Assembly.GetName().Version == new Version(1, 6, 15, 24356)
                && typeof(IModHelper).Assembly.GetName().Version == new Version(4, 4, 0, 0)
                && HashMatches(typeof(Game1).Assembly, "GameSha256") && HashMatches(typeof(IModHelper).Assembly, "SmapiSha256");
            Require((find.Invoke(null, null) is not null) == expected,
                "The verified native callback layout should be available only for the pinned SDK pair.");
        });

        bool HashMatches(Assembly assembly, string field)
        {
            string expected = (string)(adapter.GetField(field, flags)?.GetRawConstantValue()
                ?? throw new MissingFieldException(adapter.FullName, field));
            using var stream = File.OpenRead(assembly.Location);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream)) == expected;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
