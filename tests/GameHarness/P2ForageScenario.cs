using System.Diagnostics;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Locations;
using StardewValley.TerrainFeatures;
using Tile = xTile.Dimensions.Location;

namespace GameHarness;

/// <summary>Samples the installed content through native spawn and pickup on one disposable world, without saving.</summary>
internal sealed class P2ForageScenario
{
    internal const string Scenario = "P2Forage", FarmName = "XYSP2Forage";
    internal const ulong FixtureId = 620261128;
    private const string Prefix = "(O)Sora6A.XiangyeShiling_", Onion = "(O)399";
    private const int SpringCycles = 2, DaysPerSeason = 28, DaysPerYear = DaysPerSeason * 4;
    private const int SummerSamples = 14, PreparedSlots = 36;
    private static readonly string SaveName = $"{FarmName}_{FixtureId}";
    private static readonly string[] Custom = { Prefix + "BambooShoots", Prefix + "WildScallion", Prefix + "ChineseToon", Prefix + "Mugwort" };
    private static readonly MapProof[] Maps =
    {
        new("BusStop", 11, 23, new Dictionary<string, double> { ["(O)18"] = .9, ["(O)20"] = .4, ["(O)22"] = .7 },
            new[] { "(O)396", "(O)398", "(O)402" }),
        new("Mountain", 2, 13, new Dictionary<string, double> { ["(O)20"] = .7, ["(O)16"] = .5 },
            new[] { "(O)396", "(O)398" }),
        new("Forest", 68, 1, new Dictionary<string, double> { ["(O)16"] = .9, ["(O)22"] = .9 },
            new[] { "(O)396", "(O)402" })
    };
    private static readonly Vector2[] Neighbors = { Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY, Vector2.UnitX };

    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Stopwatch Watch = new();
    private readonly List<Pickup> Pickups = new();
    private readonly List<Sample> Samples = new();
    private readonly List<Pool> Pools = new();
    private readonly HashSet<(Vector2 Tile, string Item)> Inaccessible = new();
    private readonly HashSet<(Vector2 Tile, string Item)> SpawnedThisRun = new();
    private int LoadDelay = -1, MapIndex, Seed, SummerSeed, StartDay;
    private uint StartDaysPlayed;
    private bool Loaded, Started, WarpRequested, OnionRefreshAttempted, Finished, SavingObserved;
    private string? Failure;

    internal P2ForageScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && !config.OpenPreview && config.Scenario == Scenario && config.FarmName == FarmName
            && config.SaveFixtureId == FixtureId, "Bind P2 forage to the exact disposable farm.");
        this.Helper = helper;
        this.Monitor = monitor;
    }

    internal void QueueLoad() => this.LoadDelay = 180;
    internal void OnLoaded() => this.Loaded = true;
    internal void OnSaving()
    {
        if (this.Finished) return;
        this.SavingObserved = true;
        this.Failure = "Unexpected native save during the in-memory forage sample.";
    }
    internal void OnReturnedToTitle()
    {
        if (this.Loaded && !this.Finished) this.Failure = "Returned to title before the forage sample completed.";
    }

    internal void Tick()
    {
        if (this.Finished)
        {
            if (!Context.IsWorldReady || IsBoundWorld(Game1.player)) Game1.game1.Exit();
            return;
        }
        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, SaveName, SaveName)),
                    "The exact forage save is absent or another world is loaded.");
                this.Watch.Start();
                Game1.activeClickableMenu = null;
                SaveGame.Load(SaveName);
                return;
            }
            Require(this.Failure is null, this.Failure ?? "The forage sample failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(3), $"Timed out on map {this.MapIndex}, sample {this.Seed}.");
            if (!this.Loaded || !Context.IsWorldReady) return;

            Farmer player = Game1.player;
            Require(IsBoundWorld(player), "The loaded farm or farmer differs from the fixed fixture.");
            if (Game1.isWarping || Game1.fadeToBlack || !Context.IsPlayerFree) return;

            if (!this.Started)
            {
                Require(Game1.year == 1 && Game1.season == Season.Spring && Game1.dayOfMonth is >= 1 and <= 4
                    && Custom.All(id => player.Items.CountId(id) == 0), "Use a Spring 1–4 Year 1 seed without P2 forage.");
                this.StartDay = Game1.dayOfMonth;
                this.StartDaysPlayed = Game1.stats.DaysPlayed;
                this.VerifyPatchedData();
                player.maxItems.Value = PreparedSlots;
                player.Items.OverwriteWith(new Item[PreparedSlots]);
                this.Started = true;
            }

            var map = Maps[this.MapIndex];
            GameLocation location = ReadMap(map.Name);
            if (!ReferenceEquals(Game1.currentLocation, location) || !ReferenceEquals(player.currentLocation, location))
            {
                if (!this.WarpRequested)
                {
                    this.WarpRequested = true;
                    Game1.warpFarmer(map.Name, map.X, map.Y, 2);
                }
                return;
            }

            if (this.PickGroundForage(location, player, map)) return;
            if (map.Name == "Forest" && !this.Pickups.Any(pickup => pickup.Item == Onion))
            {
                if (this.PickSpringOnion(location, player)) return;
                if (!this.OnionRefreshAttempted)
                {
                    this.OnionRefreshAttempted = true;
                    // Forest.DayUpdate creates the special HoeDirt crop. It is independent of Data/Locations.Forage.
                    location.DayUpdate(Game1.dayOfMonth);
                    return;
                }
            }

            if (this.Seed < SeasonSpringSampleCount(this.StartDay))
            {
                this.Spawn(location, map, Season.Spring, this.Seed++);
                return;
            }
            bool summerSpawned = this.Spawn(location, map, Season.Summer, this.SummerSeed++);
            Require(summerSpawned || this.SummerSeed < SummerSamples,
                $"No non-custom native summer spawn witnessed on {map.Name} in the bounded sample.");
            if (!summerSpawned) return;
            if (++this.MapIndex == Maps.Length)
            {
                string[] required = Custom.Concat(Maps.SelectMany(entry => entry.Vanilla.Keys)).Append(Onion).Distinct().ToArray();
                string[] missing = required.Where(id => !this.Pickups.Any(pickup => pickup.Item == id)).ToArray();
                string[] emptyMaps = Maps.Where(entry => !this.Pickups.Any(pickup =>
                    pickup.Map == entry.Name && Custom.Contains(pickup.Item) && pickup.GeneratedBySample)).Select(entry => entry.Name).ToArray();
                Require(missing.Length == 0 && emptyMaps.Length == 0,
                    $"Bounded sample did not cover everything. Missing items [{string.Join(", ", missing)}]; no new custom pickup [{string.Join(", ", emptyMaps)}].");
                this.Finish(null);
                return;
            }
            this.Seed = 0;
            this.SummerSeed = 0;
            this.Inaccessible.Clear();
            this.SpawnedThisRun.Clear();
            this.WarpRequested = false;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { this.Finish(error.Message); }
    }

    private void VerifyPatchedData()
    {
        Dictionary<string, LocationData> locations = DataLoader.Locations(Game1.content);
        foreach (var map in Maps)
        {
            LocationData data = locations[map.Name];
            var spring = data.Forage.Where(entry => entry.Season is null or Season.Spring).ToArray();
            var custom = spring.Where(MentionsCustom).ToArray();
            Require(data.MinDailyForageSpawn == 1 && data.MaxDailyForageSpawn == 4 && data.MaxSpawnedForageAtOnce == 6
                && map.Vanilla.All(pair => spring.Count(entry => entry.Id == pair.Key && entry.ItemId == pair.Key
                    && entry.Season == Season.Spring && entry.Chance == pair.Value && entry.Condition is null) == 1),
                $"Original spring rows or native forage limits differ on {map.Name}.");
            Require(custom.Length == 1 && data.Forage.Count(MentionsCustom) == 1 && custom[0].Season == Season.Spring
                && custom[0].Condition is null && custom[0].Id.StartsWith("Sora6A.XiangyeShiling_", StringComparison.Ordinal)
                && custom[0].ItemId is null && custom[0].RandomItemId is { Count: 4 } ids
                && ids.ToHashSet(StringComparer.Ordinal).SetEquals(Custom) && custom[0].Chance is > 0 and <= 1,
                $"The additive spring pool is missing or malformed on {map.Name}.");
            this.Pools.Add(new(map.Name, custom[0].Id, custom[0].Chance, custom[0].RandomItemId!));
            Require(!ReadMap(map.Name).objects.Values.Any(item => Custom.Contains(item.QualifiedItemId)),
                $"The input seed already contains P2 forage in {map.Name}.");
        }
        Require(locations.Where(pair => Maps.All(map => map.Name != pair.Key)).All(pair => pair.Value.Forage?.Any(MentionsCustom) != true),
            "The P2 pool leaked to a non-whitelisted location/default.");
        foreach (string id in Custom.Append(Onion))
        {
            var item = ItemRegistry.Create<StardewValley.Object>(id, allowNull: true);
            Require(item is not null && item.QualifiedItemId == id && item.isForage(), $"Missing native forage object {id}.");
        }
    }

    private static bool MentionsCustom(SpawnForageData row) =>
        Custom.Contains(row.ItemId) || row.RandomItemId?.Any(Custom.Contains) == true;

    private bool Spawn(GameLocation location, MapProof map, Season season, int index)
    {
        var before = location.objects.Pairs.ToDictionary(pair => pair.Key, pair => pair.Value);
        int beforeCounter = location.numberOfSpawnedObjectsOnMap;
        int oldYear = Game1.year;
        int oldDay = Game1.dayOfMonth;
        uint oldDays = Game1.stats.DaysPlayed;
        Season oldSeason = Game1.season;
        int springOrdinal = this.StartDay - 1 + index;
        int yearOffset = season == Season.Spring ? springOrdinal / DaysPerSeason : 0;
        int day = season == Season.Spring ? springOrdinal % DaysPerSeason + 1 : 1 + index;
        int year = oldYear + yearOffset;
        int dayOffset = yearOffset * DaysPerYear + (season == Season.Summer ? DaysPerSeason : 0) + day - this.StartDay;
        uint seed = this.StartDaysPlayed + (uint)dayOffset;
        bool weekly = season == Season.Spring && day % 7 == 0;
        bool transition = season == Season.Summer ? index == 0 : day == 1 && yearOffset > 0;
        try
        {
            // A bounded deterministic sample of the native daily PRNG, without advancing/saving the world.
            Game1.year = year;
            Game1.dayOfMonth = day;
            Game1.stats.DaysPlayed = seed;
            Game1.season = season;
            Require(location.GetSeason() == season, $"{location.Name} did not resolve the sampled season.");
            // Native pickup removes the object directly. Stardew's spawn counter is cumulative until weekly cleanup
            // or a season transition, so use those native reset paths instead of writing a synthetic counter.
            if (weekly) location.DayUpdate(day);
            else
            {
                if (transition) location.seasonUpdate(onLoad: false);
                location.spawnObjects();
            }
        }
        finally
        {
            Game1.year = oldYear;
            Game1.season = oldSeason;
            Game1.stats.DaysPlayed = oldDays;
            Game1.dayOfMonth = oldDay;
        }
        if (weekly || transition)
        {
            this.Inaccessible.Clear();
            this.SpawnedThisRun.Clear();
        }
        var additions = location.objects.Pairs.Where(pair => pair.Value.IsSpawnedObject
                && (!before.TryGetValue(pair.Key, out var old) || !ReferenceEquals(old, pair.Value)))
            .Select(pair => (pair.Key, pair.Value.QualifiedItemId)).ToArray();
        this.SpawnedThisRun.UnionWith(additions.Select(entry => (entry.Key, entry.QualifiedItemId)));
        this.Samples.Add(new(location.Name, season.ToString(), year, day, seed, beforeCounter, location.numberOfSpawnedObjectsOnMap,
            weekly ? "WeeklyDayUpdate" : transition ? "SeasonUpdateAndSpawnObjects" : "SpawnObjects",
            additions.Select(entry => new SpawnEvidence(entry.QualifiedItemId, (int)entry.Key.X, (int)entry.Key.Y)).ToArray()));
        Require(season == Season.Spring || additions.All(entry => !Custom.Contains(entry.QualifiedItemId)),
            $"Sampled a new custom object outside Spring on {location.Name}.");
        return additions.Any(entry => map.SummerVanilla.Contains(entry.QualifiedItemId));
    }

    private static int SeasonSpringSampleCount(int startDay) => SpringCycles * DaysPerSeason - (startDay - 1);

    private bool PickGroundForage(GameLocation location, Farmer player, MapProof map)
    {
        foreach (var (tile, item) in location.objects.Pairs.Where(pair => pair.Value.IsSpawnedObject
            && (Custom.Contains(pair.Value.QualifiedItemId) || map.Vanilla.ContainsKey(pair.Value.QualifiedItemId))).ToArray())
        {
            string id = item.QualifiedItemId;
            if (this.Inaccessible.Contains((tile, id))) continue;
            if (!StandAdjacent(location, player, tile))
            {
                this.Inaccessible.Add((tile, id));
                continue;
            }
            int before = player.Items.CountId(id);
            uint stats = Game1.stats.ItemsForaged;
            bool acted = location.checkAction(new Tile((int)tile.X, (int)tile.Y), Game1.viewport, player);
            int acquired = player.Items.CountId(id) - before;
            Require(acted && acquired is 1 or 2 && Game1.stats.ItemsForaged == stats + 1 && !location.objects.ContainsKey(tile),
                $"Native ground pickup failed for {id} on {location.Name} at {tile}.");
            this.Pickups.Add(new(location.Name, id, (int)tile.X, (int)tile.Y, acquired, true, this.SpawnedThisRun.Remove((tile, id))));
            return true;
        }
        return false;
    }

    private bool PickSpringOnion(GameLocation location, Farmer player)
    {
        foreach (var (tile, feature) in location.terrainFeatures.Pairs.ToArray())
        {
            if (feature is not HoeDirt { crop: { } crop } dirt || !crop.forageCrop.Value
                || crop.whichForageCrop.Value != "1" || !StandAdjacent(location, player, tile)) continue;
            int before = player.Items.CountId(Onion);
            _ = dirt.performUseAction(tile);
            int acquired = player.Items.CountId(Onion) - before;
            Require(acquired == 1 && dirt.crop is null, $"The separate native Spring Onion crop did not yield one {Onion} at {tile}.");
            this.Pickups.Add(new(location.Name, Onion, (int)tile.X, (int)tile.Y, acquired, false, this.OnionRefreshAttempted));
            return true;
        }
        return false;
    }

    private static bool StandAdjacent(GameLocation location, Farmer player, Vector2 tile)
    {
        for (int direction = 0; direction < Neighbors.Length; direction++)
        {
            Vector2 standing = tile + Neighbors[direction];
            if (!location.isTileOnMap(standing) || !location.CanItemBePlacedHere(standing, collisionMask: CollisionMask.All & ~CollisionMask.Farmers)
                || location.doesTileHaveProperty((int)standing.X, (int)standing.Y, "TouchAction", "Back") is not null) continue;
            player.setTileLocation(standing);
            player.faceDirection(direction);
            return true;
        }
        return false;
    }

    private void Finish(string? failure)
    {
        this.Finished = true;
        this.Watch.Stop();
        this.Helper.Data.WriteJsonFile("p2-forage-result.json", new
        {
            SchemaVersion = 1, Scenario, FixtureId, ProcessId = Environment.ProcessId,
            Completed = failure is null, Failure = failure, this.SavingObserved,
            Sampling = "Bounded in-memory day seeds, native spawn, weekly and season reset; native warp, prepared inventory and direct pickup; no overnight simulation.",
            this.Pools, this.Samples, this.Pickups, this.StartDay, this.StartDaysPlayed, this.OnionRefreshAttempted
        });
        this.Monitor.Log(failure ?? "P2 forage native samples completed.", failure is null ? LogLevel.Info : LogLevel.Error);
    }

    private static GameLocation ReadMap(string name) =>
        Game1.getLocationFromName(name) ?? throw new InvalidOperationException($"Missing native map {name}.");
    private static bool IsBoundWorld(Farmer player) =>
        Context.IsMainPlayer && !Context.IsMultiplayer && !Context.IsSplitScreen && Constants.SaveFolderName == SaveName
        && Game1.uniqueIDForThisGame == FixtureId && player.farmName.Value == FarmName && player.Name == FarmName;

    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }

    private sealed record MapProof(string Name, int X, int Y, Dictionary<string, double> Vanilla, string[] SummerVanilla);
    private sealed record Pool(string Map, string Id, double Chance, IReadOnlyList<string> ItemIds);
    private sealed record SpawnEvidence(string Item, int X, int Y);
    private sealed record Sample(string Map, string Season, int Year, int Day, uint DaysPlayedSeed, int CounterBefore, int CounterAfter,
        string NativeCall, IReadOnlyList<SpawnEvidence> NewObjects);
    private sealed record Pickup(string Map, string Item, int X, int Y, int Quantity, bool GroundForage, bool GeneratedBySample);
}
