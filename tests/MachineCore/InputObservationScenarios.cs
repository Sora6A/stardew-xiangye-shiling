using GameHarness;

internal static class InputObservationScenarios
{
    public static void Run(Action<string, Action> test)
    {
        static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        bool Scope(bool ready = true, bool host = true, bool split = false, int screen = 0,
            bool preview = false, string? configured = "XYSNET02", ulong id = 450604963,
            string? farm = "XYSNET02", ulong actualId = 450604963, string? folder = "XYSNET02_450604963")
            => InputObservationPolicy.ScopeMatches(ready, host, split, screen, preview, configured, id, farm, actualId, folder);

        test("input observation refuses unrelated saves clients split screens and preview", () =>
        {
            Assert(Scope(), "Dedicated host should be observable with network probes disabled.");
            foreach (bool result in new[] { Scope(ready: false), Scope(host: false), Scope(split: true),
                Scope(screen: 1), Scope(preview: true), Scope(configured: null), Scope(configured: "XYSP1UI"),
                Scope(id: 0), Scope(actualId: 99), Scope(farm: "XYSNET03"), Scope(folder: "XYSP1Save_8624092801"),
                Scope(folder: null), Scope(configured: "xysnet02", farm: "xysnet02", folder: "xysnet02_450604963") })
                Assert(!result, "An unsafe observation scope was accepted.");
        });
        test("input observation omits text and arbitrary keys", () =>
        {
            Assert(InputObservationPolicy.CanRecordKeys(false, false), "Normal gameplay key observation failed.");
            Assert(!InputObservationPolicy.CanRecordKeys(true, false)
                && !InputObservationPolicy.CanRecordKeys(false, true)
                && !InputObservationPolicy.CanRecordKeys(true, true), "Text entry was observable.");
            foreach (string key in new[] { "W", "A", "S", "D", "E", "Escape" })
                Assert(InputObservationPolicy.IsAllowedKey(key), "Required diagnostic key was omitted.");
            foreach (string key in new[] { "T", "Enter", "F8", "MouseLeft", "w", "", "private chat" })
                Assert(!InputObservationPolicy.IsAllowedKey(key), "An unrelated key/text was permitted.");
        });
        test("input observation sample schedule has monotonic duration and capacity bounds", () =>
        {
            Assert(InputObservationPolicy.CanSample(0, -1, 0), "Initial observation was lost.");
            Assert(!InputObservationPolicy.CanSample(199, 0, 1)
                && InputObservationPolicy.CanSample(200, 0, 1), "Five Hz interval was violated.");
            Assert(!InputObservationPolicy.CanSample(100, 200, 2)
                && !InputObservationPolicy.CanSample(-1, -1, 0), "Regressing time was accepted.");
            Assert(InputObservationPolicy.CanSample(179800, 179600, 899)
                && !InputObservationPolicy.CanSample(180000, 179800, 900)
                && !InputObservationPolicy.CanSample(200, 0, 900), "Duration or record bound failed.");
        });
        test("input observation limits short key event capture independently", () =>
        {
            Assert(InputObservationPolicy.CanRecordEvent(179999, 119), "Last allowed event was lost.");
            Assert(!InputObservationPolicy.CanRecordEvent(0, 120)
                && !InputObservationPolicy.CanRecordEvent(180000, 0)
                && !InputObservationPolicy.CanRecordEvent(-1, 0), "Unbounded event capture was permitted.");
        });
    }
}
