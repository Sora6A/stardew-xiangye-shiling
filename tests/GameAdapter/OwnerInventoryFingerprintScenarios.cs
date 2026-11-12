using System.Reflection;
using System.Xml.Serialization;
using Microsoft.Xna.Framework.Content;
using StardewValley;
using XiangyeShiling.GameIntegration;
using SObject = StardewValley.Object;

internal static class OwnerInventoryFingerprintScenarios
{
    internal static void Run(Action<string, Action> test)
    {
        Type adapter = typeof(MachineService).Assembly.GetType(
            "XiangyeShiling.GameIntegration.OwnerInventoryFingerprint", throwOnError: true)!;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        Type digest = typeof(string).MakeByRefType();
        MethodInfo read = RequireMethod("TryRead", typeof(Item), digest);
        MethodInfo readSlot = RequireMethod("TryReadSlot", typeof(Farmer), typeof(int), digest);
        MethodInfo readWithFactory = RequireMethod("TryRead", typeof(Item), typeof(Func<string, int, int, Item>), digest);
        string soy = "(O)" + MachineService.Prefix + "_Soybean";

        MethodInfo RequireMethod(string name, params Type[] types) => adapter.GetMethod(name, flags, null, types, null)
            ?? throw new MissingMethodException(adapter.FullName, name);
        (bool Success, string? Fingerprint) Observe(MethodInfo method, params object?[] args)
        {
            var success = (bool)method.Invoke(null, args)!;
            return (success, (string?)args[^1]);
        }
        (bool Success, string? Fingerprint) WithCanonical(Item item, Func<string, int, int, Item?>? factory = null)
            // The CLI has no content registry. Build a new Object with the native SDK and the test's canonical fields.
            => Observe(readWithFactory, item, factory ?? Plain, null);

        test("native slot fingerprint distinguishes empty from unavailable", () =>
        {
            Check(Observe(read, null, null) == (true, null), "A known empty slot must succeed with a null digest.");
            Check(Observe(readSlot, null, 0, null) == (false, null), "A missing Farmer cannot prove an empty slot.");
            var variant = new UnsupportedObject { ItemId = "262", Stack = 1 };
            Check(Observe(read, variant, null) == (false, null), "An Object subclass is not an empty or recoverable slot.");
            var metadata = Plain("(O)262", 2, 0);
            metadata.modData["Other.Mod/marker"] = "preserve";
            Check(Observe(read, metadata, null) == (false, null), "Modded metadata must not be discarded on reconstruction.");
            Check(metadata.modData["Other.Mod/marker"] == "preserve" && metadata.Stack == 2,
                "Rejected fingerprinting must leave the native item unchanged.");
        });
        test("native XML fingerprint binds qualified ID stack quality and round trip", () =>
        {
            var item = Plain(soy, 3, 2);
            var original = WithCanonical(item);
            string? value = original.Fingerprint;
            Check(original.Success && value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'),
                "A recoverable native Object must produce a lowercase SHA-256 digest.");
            Check(WithCanonical(Plain(soy, 3, 2)) == original, "Equal independent native objects must produce equal digests.");
            foreach (SObject changed in new[] { Plain(soy, 2, 2), Plain(soy, 3, 1), Plain("(O)262", 3, 2) })
            {
                var observed = WithCanonical(changed);
                Check(observed.Success && observed.Fingerprint != value, "ID, stack and quality must each affect the digest.");
            }

            var serializer = new XmlSerializer(typeof(SObject));
            using var stream = new MemoryStream();
            serializer.Serialize(stream, item);
            stream.Position = 0;
            var reloaded = (SObject)serializer.Deserialize(stream)!;
            Check(WithCanonical(reloaded) == original, "The native XML round trip must preserve the digest.");
        });
        test("native slot fingerprint declines altered native fields and unknown reconstruction", () =>
        {
            var variant = Plain(soy, 3, 2);
            variant.price.Value = 9876;
            Check(WithCanonical(variant) == (false, null), "A native price override cannot match the registry prototype.");
            Check(variant.price.Value == 9876, "Fingerprinting must not normalize the live variant.");
            var item = Plain(soy, 3, 2);
            Check(WithCanonical(item, (_, _, _) => null) == (false, null), "Missing registered content is unavailable.");
            Check(WithCanonical(item, (_, _, _) => throw new ContentLoadException("fixture unavailable")) == (false, null),
                "A registry content failure cannot certify an inventory slot.");
            Check(WithCanonical(item, (_, _, _) => item) == (false, null), "The live object cannot certify its own recovery.");
            Check(WithCanonical(Plain("(O)79", 2, 0)) == (false, null),
                "A stack larger than the native maximum cannot be recovered as an ordinary slot.");
            item.heldObject.Value = item;
            Check(WithCanonical(item) == (false, null), "Nested/cyclic item state must be rejected before serialization.");
        });
        test("ordinary inventory fingerprint ignores visual flip without changing either object", () =>
        {
            var wheat = Plain("(O)262", 2, 0);
            var canonical = Plain("(O)262", 2, 0);
            var original = WithCanonical(wheat);
            wheat.flipped.Value = true;
            var flipped = WithCanonical(wheat, (_, _, _) => canonical);
            Check(original.Success && flipped == original, "A different native sprite flip must not reject ordinary wheat.");
            Check(wheat.flipped.Value && !canonical.flipped.Value, "Fingerprinting must preserve both native objects.");
            canonical.price.Value = 9876;
            Check(WithCanonical(wheat, (_, _, _) => canonical) == (false, null),
                "Visual normalization must not mask a price difference.");
        });
    }

    private static SObject Plain(string id, int stack, int quality)
        => new() { ItemId = id[3..], Stack = stack, Quality = quality };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class UnsupportedObject : SObject { }
}
