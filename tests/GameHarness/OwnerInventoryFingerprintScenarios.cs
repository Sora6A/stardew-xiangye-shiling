using System.Reflection;
using System.Xml.Linq;
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
            bool readable = read(owner, i, out string? digest);
            if (!readable)
                throw new InvalidOperationException(DescribeFailure(type, owner.Items[i]!));
            Require(digest is { Length: 64 } && digest.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')
                && digests.Add(digest), "Live material did not have a unique lowercase SHA-256: " + itemIds[i]);
            Require(read(reloaded, i, out string? after) && after == digest,
                "Native Farmer XML changed the material fingerprint: " + itemIds[i]);
        }
        Require(read(owner, 2, out string? empty) && empty is null
            && read(reloaded, 2, out string? loadedEmpty) && loadedEmpty is null,
            "An empty slot must remain distinct from unavailable before and after native XML.");
        Require(!read(null, 2, out _) && !read(owner, owner.MaxItems, out _),
            "An unavailable owner or invalid index cannot certify an empty slot.");
    }

    private static string DescribeFailure(Type type, Item item)
    {
        var canonical = ItemRegistry.Create(item.QualifiedItemId, item.Stack, item.Quality);
        string shape = $"{item.QualifiedItemId} {item.GetType().Name}/{canonical.GetType().Name} stack={item.Stack}/{item.maximumStackSize()} quality={item.Quality} metadata={item.modData.Pairs.Count()}";
        if (item is not StardewValley.Object || canonical is not StardewValley.Object) return shape;
        MethodInfo serialize = type.GetMethod("WriteNativeXml", BindingFlags.Static | BindingFlags.NonPublic)!;
        XElement Read(Item value) => XElement.Load(new MemoryStream((byte[])serialize.Invoke(null, new object[] { value })!));
        XElement current = Read(item), expected = Read(canonical);
        var changed = current.Elements().Select(x => x.Name).Union(expected.Elements().Select(x => x.Name))
            .Where(name => !XNode.DeepEquals(current.Element(name), expected.Element(name)));
        return shape + " XML fields: " + string.Join(", ", changed);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
