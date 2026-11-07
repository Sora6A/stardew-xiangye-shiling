using XiangyeShiling.Machines;
using XiangyeShiling.Networking;

internal static class ProtocolScenarios
{
    public static void Run(Action<string, Action> test)
    {
        var state = MachineState.Empty(Guid.NewGuid(), MachineKind.BeanWorkshop);
        var request = new MachineRequestEnvelope(1, Guid.NewGuid(), Guid.NewGuid(), 10, "FarmHouse", 8, 7,
            state.InstanceId, 0, MachineRequestAction.Start, "tofu");
        var actor = new HostActorSnapshot(request.SessionId, true, true, true, true, 10, 10, "FarmHouse", 7, 7, false);
        var target = new HostTargetSnapshot("FarmHouse", 8, 7, state);
        string json = MachineRequestCodec.Write(request);
        static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        void Reject(MachineRequestEnvelope intent, HostActorSnapshot source, HostTargetSnapshot? machine,
            HostRequestRejection reason)
        {
            HostAuthorization result = HostRequestGate.Authorize(intent, source, machine);
            Assert(!result.Authorized && result.Rejection == reason && result.RuleRequest is null,
                $"Expected {reason} with no executable rule request, got {result.Rejection}.");
        }
        test("protocol roundtrip preserves bound start and collect intents", () =>
        {
            Assert(MachineRequestCodec.TryRead(json, out var loaded) && loaded == request, "Start intent changed.");
            var collect = request with { Action = MachineRequestAction.Collect, RecipeId = null };
            Assert(MachineRequestCodec.TryRead(MachineRequestCodec.Write(collect), out var second) && second == collect,
                "Collect intent changed or invented a recipe.");
        });
        test("protocol malformed unicode is rejected without throwing or replacing the request", () =>
        {
            foreach (string malformed in new[] {
                json.Replace("\"Action\":\"Start\"", "\"Action\":\"\\uD800\""),
                json.Replace(request.SessionId.ToString(), "\\uD800"),
                json.Replace("FarmHouse", "\\uDC00"),
                json.Replace("FarmHouse", "\uD800"),
                json.Replace("\"RecipeId\":\"tofu\"", "\"RecipeId\":\"\\uD800\""),
                json.Replace("\"ProtocolVersion\"", "\"\\uD800\"") })
            {
                Assert(malformed != json, "Malformed unicode fixture did not change the original.");
                Assert(!MachineRequestCodec.TryRead(malformed, out var parsed) && parsed is null,
                    "BUG-2026-013: malformed Unicode must return false/null, never throw or substitute an intent.");
            }
            Assert(!MachineRequestCodec.IsValid(request with { LocationName = "\uD800" })
                && !MachineRequestCodec.IsValid(request with { RecipeId = "\uDC00" }),
                "BUG-2026-013: incomplete UTF-16 in direct intents must not be normalized on write.");
            bool writeRejected = false;
            try { MachineRequestCodec.Write(request with { RecipeId = "\uDC00" }); }
            catch (ArgumentException) { writeRejected = true; }
            Assert(writeRejected, "Invalid direct request was serialized instead of rejected.");
            var validUnicode = request with { LocationName = "农舍🌱" };
            Assert(MachineRequestCodec.TryRead(MachineRequestCodec.Write(validUnicode), out var unicode)
                && unicode == validUnicode, "Valid surrogate pairs or Chinese location names were damaged.");
        });
        test("protocol rejects ambiguous fields and client-supplied inventory or time", () =>
        {
            string[] invalid = { "null", "[]", "{", json.Replace("\"ProtocolVersion\":1", "\"ProtocolVersion\":2"),
                json.Replace("\"ProtocolVersion\":1", "\"ProtocolVersion\":1,\"ProtocolVersion\":1"),
                json.Replace("\"ProtocolVersion\":1,", ""),
                json.Replace("\"ProtocolVersion\":1", "\"ProtocolVersion\":1,\"Inventory\":[]"),
                json.Replace("\"ProtocolVersion\":1", "\"ProtocolVersion\":1,\"Now\":100"),
                json.Replace("\"ActorId\":10", "\"ActorId\":\"10\""),
                json.Replace("\"Action\":\"Start\"", "\"Action\":\"start\""),
                json.Replace("\"Action\":\"Start\"", "\"Action\":0"),
                json.Replace(request.SessionId.ToString(), request.SessionId.ToString("N")) };
            foreach (string malformed in invalid)
            {
                Assert(malformed != json, "Malformed fixture failed to change the valid request.");
                Assert(!MachineRequestCodec.TryRead(malformed, out var parsed) && parsed is null,
                    "Ambiguous or client-owned data was accepted.");
            }
        });
        test("protocol bounds messages and rejects contradictory in-memory intents", () =>
        {
            Assert(!MachineRequestCodec.TryRead(new string(' ', MachineRequestCodec.MaxUtf8Bytes + 1), out var parsed)
                && parsed is null, "Oversized wire input accepted.");
            // Unicode input fits the character count but exceeds the UTF-8 limit.
            string unicode = "{\"" + new string('豆', 1400) + "\":0}";
            Assert(unicode.Length < MachineRequestCodec.MaxUtf8Bytes
                && !MachineRequestCodec.TryRead(unicode, out parsed), "Byte limit was treated as character limit.");
            foreach (var invalid in new[] { request with { RequestId = Guid.Empty }, request with { ActorId = 0 },
                request with { ExpectedRevision = -1 }, request with { TileX = -1 },
                request with { LocationName = new string('a', 129) }, request with { RecipeId = " tofu" },
                request with { Action = (MachineRequestAction)20 },
                request with { Action = MachineRequestAction.Collect }, request with { RecipeId = null } })
                Assert(!MachineRequestCodec.IsValid(invalid), "Invalid direct intent bypassed codec validation.");
        });
        test("host gate authenticates sender and inventory owner before allowing a rule request", () =>
        {
            Reject(request with { ActorId = 20 }, actor, target, HostRequestRejection.ActorMismatch);
            Reject(request, actor with { InventoryOwnerId = 20 }, target, HostRequestRejection.InventoryOwnerMismatch);
            Reject(request, actor with { ActorConnected = false }, target, HostRequestRejection.ActorOffline);
            Reject(request with { SessionId = Guid.NewGuid() }, actor, target, HostRequestRejection.WrongSession);
            HostAuthorization allowed = HostRequestGate.Authorize(request, actor, target);
            Assert(allowed.Authorized && allowed.RuleRequest == new Request(request.RequestId, state.InstanceId, 0, 10),
                "Authenticated request lost its original identity or revision.");
        });
        test("host gate requires game thread loaded host world and active actor", () =>
        {
            Reject(request, actor with { IsOnGameThread = false }, target, HostRequestRejection.NotGameThread);
            Reject(request, actor with { IsMainPlayer = false }, target, HostRequestRejection.NotHost);
            Reject(request, actor with { IsWorldReady = false }, target, HostRequestRejection.WorldUnavailable);
            Reject(request, actor with { ActorBusy = true }, target, HostRequestRejection.ActorBusy);
            Assert(!HostRequestGate.Authorize(request, null, target).Authorized, "Missing trusted context accepted.");
        });
        test("host gate requires actual target address same map range and valid state", () =>
        {
            Reject(request, actor, null, HostRequestRejection.TargetMissing);
            Reject(request, actor, target with { TileX = 9 }, HostRequestRejection.TargetMismatch);
            Reject(request, actor with { LocationName = "Town" }, target, HostRequestRejection.WrongLocation);
            Reject(request, actor with { TileX = 3 }, target, HostRequestRejection.OutOfRange);
            Reject(request with { TileX = int.MaxValue }, actor, target with { TileX = int.MaxValue },
                HostRequestRejection.OutOfRange);
            Reject(request, actor, target with { State = null }, HostRequestRejection.InvalidTargetState);
            Reject(request, actor, target with { State = state with { SchemaVersion = 2 } }, HostRequestRejection.InvalidTargetState);
            Assert(state.Revision == 0 && state.Batch is null, "Authorization mutated target state.");
        });
        test("authorized requests still require rule checks for revision identity and recipe", () =>
        {
            InventorySlot?[] inventory = { new("(O)Sora6A.XiangyeShiling_Soybean", 3, 0, 999), null };
            var recipe = new MachineRecipe("tofu", MachineKind.BeanWorkshop,
                new[] { new Ingredient("(O)Sora6A.XiangyeShiling_Soybean", 3) },
                new("(O)Sora6A.XiangyeShiling_Tofu", 2, 0, 999), 120);
            Request old = HostRequestGate.Authorize(request with { ExpectedRevision = 1 }, actor, target).RuleRequest!;
            Assert(MachineRules.Start(state, old, recipe, true, inventory, 100).Rejection == Rejection.StaleRevision,
                "Authorization substituted the current revision for a stale client intent.");
            Request wrong = HostRequestGate.Authorize(request with { InstanceId = Guid.NewGuid() }, actor, target).RuleRequest!;
            Assert(MachineRules.Start(state, wrong, recipe, true, inventory, 100).Rejection == Rejection.WrongInstance,
                "Authorization rebound a removed machine's request.");
            Request valid = HostRequestGate.Authorize(request, actor, target).RuleRequest!;
            Assert(MachineRules.Start(state, valid, recipe, false, inventory, 100).Rejection == Rejection.LockedRecipe,
                "Authorization invented recipe unlock ownership.");
            Assert(inventory[0]!.Quantity == 3 && state.Revision == 0, "Rejected rule plan changed authoritative snapshots.");
        });
    }
}
