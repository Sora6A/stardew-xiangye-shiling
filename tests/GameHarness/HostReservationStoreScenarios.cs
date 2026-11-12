using System.Reflection;
using System.Text.Json;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Networking;
using SObject = StardewValley.Object;

namespace GameHarness;

internal static class HostReservationStoreScenarios
{
    private const string Key = MachineService.Prefix + "/HostReservation";

    internal static void Run(Farmer host, SObject machine, MachineService service)
    {
        Require(!host.modData.ContainsKey(Key), "The fixture must have no existing host reservation.");
        object store = typeof(MachineService).GetProperty("Reservations", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;
        object? Call(string name, params object?[] args) => store.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(store, args);
        bool Read(out object? reservation)
        {
            var arguments = new object?[] { null };
            bool readable = (bool)Call("TryRead", arguments)!;
            reservation = arguments[0];
            return readable;
        }

        Require(Read(out var absent) && absent is null, "Missing host data must remain a legacy empty reservation.");
        GameLocation location = host.currentLocation;
        try
        {
            const string unknown = "{\"SchemaVersion\":2}";
            host.modData[Key] = unknown;
            Require(!Read(out _) && location.removeObject(machine.TileLocation, false) is null
                && ReferenceEquals(location.objects[machine.TileLocation], machine) && host.modData[Key] == unknown,
                "An unknown reservation must block actual removal and retain the original data.");

            host.modData[Key] = CreateFixtureRecord(host, machine, service);
            Require(Read(out var plan) && plan is not null, "The actual host could not read its valid reservation.");
            object terminal = Enum.Parse(plan!.GetType().GetProperty("Status")!.PropertyType, "Cancelled");
            host.modData.Remove(Key);
            Call("OnSaving");
            Require(!Read(out _) && !(bool)Call("TryReserve", plan)!, "No reservation may start while saving.");
            Call("OnSaved");
            Require((bool)Call("TryReserve", plan)! && !(bool)Call("TryReserve", plan)!,
                "A real host must write a reservation once and retain ownership of its slot.");

            string raw = host.modData[Key];
            using var xml = new MemoryStream();
            SaveGame.farmerSerializer.Serialize(xml, host);
            xml.Position = 0;
            var reloaded = (Farmer)SaveGame.farmerSerializer.Deserialize(xml)!;
            Require(reloaded.modData.GetValueOrDefault(Key) == raw, "Native Farmer XML changed the reservation bytes.");
            Require(location.removeObject(machine.TileLocation, false) is null && ReferenceEquals(location.objects[machine.TileLocation], machine),
                "An idle machine with a reservation must remain in place.");
            Require((bool)Call("TryRecordTerminal", plan, terminal)! && !(bool)Call("TryRecordTerminal", plan, terminal)!
                && Read(out var ended) && ended?.GetType().GetProperty("Status")!.GetValue(ended)?.ToString() == "Cancelled",
                "The host must record the same plan's terminal status once.");
        }
        finally
        {
            Call("OnSaved");
            host.modData.Remove(Key);
            if (!location.objects.ContainsKey(machine.TileLocation)) location.objects[machine.TileLocation] = machine;
        }
    }

    private static string CreateFixtureRecord(Farmer host, SObject machine, MachineService service)
    {
        string beforeRaw = machine.modData[MachineService.StateKey];
        Require(MachineStateCodec.TryRead(beforeRaw, out var before), "The fixture machine must be initialized.");
        MachineRecipe recipe = service.Recipes.First(value => value.Machine == before!.Kind);
        long actor = host.UniqueMultiplayerID == 1 ? 2 : 1;
        var request = new MachineRequestEnvelope(1, Guid.NewGuid(), Guid.NewGuid(), actor, host.currentLocation.NameOrUniqueName,
            (int)machine.TileLocation.X, (int)machine.TileLocation.Y, before!.InstanceId, before.Revision, MachineRequestAction.Start, recipe.Id);
        var stock = recipe.Inputs.Select(i => (InventorySlot?)new(i.ItemId, i.Quantity, 0, 999)).ToArray();
        MachineTransaction plan = MachineRules.Start(before, new(request.RequestId, request.InstanceId, before.Revision, actor),
            recipe, true, stock, MachineService.Now()).Transaction ?? throw new InvalidOperationException("The fixture plan was rejected.");
        return JsonSerializer.Serialize(new
        {
            SchemaVersion = 1, WorldId = Game1.uniqueIDForThisGame, HostId = host.UniqueMultiplayerID,
            GrantId = Guid.NewGuid(), Status = "Reserved", RequestRaw = MachineRequestCodec.Write(request),
            BeforeRaw = beforeRaw, AfterRaw = MachineStateCodec.Write(plan.After), Capacity = host.MaxItems,
            Changes = plan.Slots.Select(change => new { Index = change.Slot, Before = new string('a', 64), After = (string?)null })
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
