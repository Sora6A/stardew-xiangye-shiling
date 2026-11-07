using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using SObject = StardewValley.Object;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < args.Length; i += 2)
            {
                if (i + 1 == args.Length || !options.TryAdd(args[i], Path.GetFullPath(args[i + 1])))
                    throw new ArgumentException("Expected unique --game-path, --smapi-path and --production path arguments.");
            }
            string gamePath = options["--game-path"], smapiPath = options["--smapi-path"];
            string production = options["--production"];
            if (!File.Exists(production) || !File.Exists(Path.Combine(gamePath, "Stardew Valley.dll"))
                || !File.Exists(Path.Combine(smapiPath, "StardewModdingAPI.dll")))
                throw new FileNotFoundException("Actual production build and complete local game/SMAPI SDK are required.");
            string[] roots = { Path.GetDirectoryName(production)!, gamePath, smapiPath,
                Path.Combine(smapiPath, "smapi-internal") };
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                // Resolve only managed assemblies from the explicitly supplied local installation/build.
                // No game process, asset bootstrap, saved world or synthetic SDK implementation is involved.
                string? file = roots.Select(root => Path.Combine(root, name.Name + ".dll")).FirstOrDefault(File.Exists);
                return file is null ? null : context.LoadFromAssemblyPath(file);
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(production);
            return RunCases(production);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SETUP FAILED {ex.GetType().Name}: {ex.Message}");
            return 2;
        }
    }

    // Keep SDK-bound code out of Main's JIT until the explicit local assembly resolver is installed.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunCases(string production)
    {
        string selectedProduction = Path.GetFullPath(typeof(MachineService).Assembly.Location);
        if (!string.Equals(selectedProduction, production, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved production assembly differs from the explicitly requested file.");
        Console.WriteLine("PRODUCTION_PATH=" + selectedProduction);
        Console.WriteLine("PRODUCTION_SHA256=" + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(selectedProduction))).ToLowerInvariant());
        int passed = 0, failed = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        void Test(string name, Action action)
        {
            try { action(); Console.WriteLine("PASS " + name); passed++; }
            catch (Exception ex)
            {
                while (ex is TargetInvocationException { InnerException: not null } invocation)
                    ex = invocation.InnerException!;
                Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                failed++;
            }
        }
        MethodInfo Prefix() => typeof(MachineService).Assembly
            .GetType("XiangyeShiling.GameIntegration.MachinePatches", throwOnError: true)!
            .GetMethod("ProtectNpc", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException("Actual production ProtectNpc prefix is missing.");
        void CheckPrefix(SObject? item, bool protectedItem)
        {
            foreach (bool original in new[] { false, true })
            {
                object?[] parameters = { item, original };
                Check(Prefix().Invoke(null, parameters) is bool runNative && runNative != protectedItem,
                    "NPC prefix must run native only for unprotected inputs.");
                Check(parameters[1] is bool value && value == (protectedItem ? false : original),
                    "NPC prefix must preserve pass-through results or explicitly reject protected removal.");
            }
        }
        bool InvokePatchedNpc(SObject? item)
        {
            // Null short-circuits in the original SDK method; protected objects must skip it in the prefix.
            // No unprotected non-null object may enter this fixture's uninitialized map/world.
            var target = AccessTools.Method(typeof(GameLocation), "checkDestroyItem",
                new[] { typeof(SObject), typeof(Vector2), typeof(bool) })
                ?? throw new MissingMethodException("Actual native checkDestroyItem signature is missing.");
            string id = "Sora6A.XiangyeShiling.Tests.NullNpc." + Guid.NewGuid().ToString("N");
            var harmony = new Harmony(id);
            try
            {
                harmony.Patch(target, new HarmonyMethod(Prefix()));
                var location = (GameLocation)RuntimeHelpers.GetUninitializedObject(typeof(GameLocation));
                return target.Invoke(location, new object?[] { item, Vector2.Zero, false }) is bool result
                    ? result : throw new InvalidOperationException("Native method did not return a boolean.");
            }
            finally { harmony.UnpatchAll(id); }
        }
        SObject CustomObject(string key)
        {
            // Actual SDK parameterless objects and net fields, with no ItemRegistry/assets bootstrap.
            var item = new SObject { ItemId = MachineService.Prefix + "_" + key };
            item.bigCraftable.Value = true;
            return item;
        }
        void CheckMachineStates(string name, Func<MachineState, MachineState?> stateFor,
            bool protect, MachinePhase? phase = null, long at = 0,
            Func<MachineState, string?>? rawFor = null)
        {
            Test(name, () =>
            {
                foreach (var (key, kind) in new[] { ("BeanWorkshop", MachineKind.BeanWorkshop),
                    ("FermentationCrock", MachineKind.FermentationCrock) })
                {
                    var item = CustomObject(key);
                    var empty = MachineState.Empty(Guid.NewGuid(), kind);
                    var expectedState = stateFor(empty);
                    string? raw = rawFor is null ? (expectedState is null ? "{invalid" : MachineStateCodec.Write(expectedState))
                        : rawFor(empty);
                    if (raw is not null) item.modData[MachineService.StateKey] = raw;
                    item.modData["Other.Mod/untouched"] = "sentinel";
                    var originalData = item.modData;
                    string originalId = item.QualifiedItemId;
                    Check(MachineService.TryGetKind(item, out var actualKind) && actualKind == kind,
                        "Actual SDK object must retain its exact custom machine identity.");
                    bool readable = MachineService.TryRead(item, out var observedState);
                    Check(readable == (expectedState is not null), "Only valid state may be read.");
                    if (phase is not null)
                        Check(observedState!.PhaseAt(at) == phase, "Fixture must have the intended actual state phase.");
                    Check(MachineService.BlocksRemoval(item) == protect, "Existing state protection must be preserved.");
                    CheckPrefix(item, protect);
                    if (protect) Check(!InvokePatchedNpc(item), "Registered prefix must reject protected native removal.");
                    Check(raw is null ? !item.modData.ContainsKey(MachineService.StateKey)
                        : item.modData.TryGetValue(MachineService.StateKey, out var observedRaw)
                            && observedRaw == raw && ReferenceEquals(observedRaw, raw),
                        "Classification/removal checks must preserve absent state or the original raw string reference.");
                    Check(ReferenceEquals(item.modData, originalData) && item.QualifiedItemId == originalId
                        && item.modData["Other.Mod/untouched"] == "sentinel" && item.modData.Count() == (raw is null ? 1 : 2),
                        "Classification/removal checks must leave identity, metadata reference and unrelated values untouched.");
                }
            });
        }
        MachineState Started(MachineState empty)
        {
            var recipe = new MachineRecipe("adapter-regression", empty.Kind,
                new[] { new Ingredient("(O)388", 1) }, new Product("(O)390", 1, 0, 999), 100);
            var request = new Request(Guid.NewGuid(), empty.InstanceId, empty.Revision, 10);
            var decision = MachineRules.Start(empty, request, recipe, true,
                new InventorySlot?[] { new("(O)388", 1, 0, 999), null }, 100);
            Check(decision.Accepted && decision.Transaction is not null, "Real rules must construct a valid busy fixture.");
            return decision.Transaction!.After;
        }

        Test("null object is not a custom machine", () =>
            Check(!MachineService.TryGetKind(null!, out _), "Null must not be classified as a machine."));
        Test("null object has no machine state", () =>
        {
            Check(!MachineService.TryRead(null!, out var state), "Null must not provide machine state.");
            Check(state is null, "Rejected null must leave the state output null.");
        });
        Test("null object does not block removal", () =>
            Check(!MachineService.BlocksRemoval(null!), "Empty tiles must not receive machine removal protection."));
        Test("actual NPC prefix passes null through without replacing native result", () =>
            CheckPrefix(null, protectedItem: false));
        Test("registered production NPC prefix preserves native empty-tile false", () =>
            Check(!InvokePatchedNpc(null), "Patched native null path must keep its original false result."));
        Test("ordinary O and BC SDK objects pass through without state or metadata changes", () =>
        {
            foreach (bool bigCraftable in new[] { false, true })
            {
                var item = new SObject { ItemId = bigCraftable ? "12" : "388" };
                item.bigCraftable.Value = bigCraftable;
                string raw = new("not-a-machine".ToCharArray());
                item.modData[MachineService.StateKey] = raw;
                var originalData = item.modData;
                string originalId = item.QualifiedItemId;
                Check(!MachineService.TryGetKind(item, out _), "Ordinary object must not become a custom machine.");
                Check(!MachineService.TryRead(item, out var state) && state is null,
                    "Ordinary objects must not interpret arbitrary machine-key metadata.");
                Check(!MachineService.BlocksRemoval(item), "Ordinary native behavior must remain available.");
                CheckPrefix(item, protectedItem: false);
                Check(item.modData[MachineService.StateKey] == raw && ReferenceEquals(item.modData[MachineService.StateKey], raw)
                    && ReferenceEquals(item.modData, originalData) && item.QualifiedItemId == originalId && item.modData.Count() == 1,
                    "Pass-through must preserve ordinary identity, raw string and metadata references.");
            }
        });
        CheckMachineStates("both custom idle SDK objects remain movable", state => state,
            protect: false, phase: MachinePhase.Idle, at: 100);
        CheckMachineStates("both custom processing SDK objects remain protected", Started,
            protect: true, phase: MachinePhase.Processing, at: 100);
        CheckMachineStates("both custom ready SDK objects remain protected", Started,
            protect: true, phase: MachinePhase.Ready, at: 200);
        CheckMachineStates("both custom corrupt SDK objects retain raw and remain protected", _ => null, protect: true);
        CheckMachineStates("both custom missing-state SDK objects remain protected without initialization", _ => null,
            protect: true, rawFor: _ => null);
        CheckMachineStates("both custom future-state SDK objects retain raw and remain protected", _ => null,
            protect: true, rawFor: state =>
            {
                var json = System.Text.Json.Nodes.JsonNode.Parse(MachineStateCodec.Write(state))!;
                json["SchemaVersion"] = 999;
                return json.ToJsonString();
            });
        CheckMachineStates("both custom mismatched-kind SDK objects retain raw and remain protected", _ => null,
            protect: true, rawFor: state => MachineStateCodec.Write(state with
            {
                Kind = state.Kind == MachineKind.BeanWorkshop ? MachineKind.FermentationCrock : MachineKind.BeanWorkshop
            }));
        Console.WriteLine($"RESULT {passed} passed, {failed} failed; no game process or save was opened.");
        return failed == 0 ? 0 : 1;
    }
}
