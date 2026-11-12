using System.Reflection;
using StardewValley;
using XiangyeShiling.GameIntegration;

namespace GameHarness;

internal static class OwnerInventoryFingerprintScenarios
{
    internal const string FarmName = "XYSP1Menu";
    internal const ulong WorldId = 1288856040646931861;
    internal const string SaveFolder = "XYSP1Menu_1288856040646931861";

    private delegate bool ReadSlot(Farmer? owner, int slot, out string? fingerprint);

    internal static void Run(Farmer owner)
    {
        Type type = typeof(MachineService).Assembly.GetType(
            "XiangyeShiling.GameIntegration.OwnerInventoryFingerprint", throwOnError: true)!;
        MethodInfo method = type.GetMethod("TryReadSlot", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, "TryReadSlot");
        var read = method.CreateDelegate<ReadSlot>();

        // Use the same native Farmer serializer as the game. This round trip stays in memory.
        using var xml = new MemoryStream();
        SaveGame.farmerSerializer.Serialize(xml, owner);
        xml.Position = 0;
        var reloaded = (Farmer?)SaveGame.farmerSerializer.Deserialize(xml)
            ?? throw new InvalidOperationException("Native Farmer XML could not be reloaded.");

        string[] itemIds = { "(O)" + MachineService.Prefix + "_Soybean", "(O)262" };
        var digests = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < itemIds.Length; i++)
        {
            Require(owner.Items[i]?.QualifiedItemId == itemIds[i], "Wrong live registry material in the test slot.");
            Require(read(owner, i, out string? digest) && digest is { Length: 64 }
                && digest.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') && digests.Add(digest),
                "Live material was unavailable or did not have a unique lowercase SHA-256: " + itemIds[i]);
            Require(read(reloaded, i, out string? after) && after == digest,
                "Native Farmer XML changed the material fingerprint: " + itemIds[i]);
        }
        Require(read(owner, 2, out string? empty) && empty is null
            && read(reloaded, 2, out string? loadedEmpty) && loadedEmpty is null,
            "An empty slot must remain distinct from unavailable before and after native XML.");
        Require(!read(null, 2, out _) && !read(owner, owner.MaxItems, out _),
            "An unavailable owner or invalid index cannot certify an empty slot.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
