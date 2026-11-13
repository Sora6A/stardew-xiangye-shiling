using XiangyeShiling.Networking;

internal static class HostOwnerIntentCapacityScenarios
{
    internal static void Run(Action<string, Action> test)
    {
        test("host intent capacity refuses fresh requests after the hard limit and still preserves an active grant", () =>
        {
            var capacity = new HostOwnerIntentCapacity();
            var active = (Actor: 1L, Request: Guid.NewGuid());
            for (int i = 0; i < 255; i++)
            {
                var prior = (Actor: 2L, Request: Guid.NewGuid());
                Expect(HostOwnerIntentDisposition.Prepare, capacity.Decide(prior, null));
                capacity.Remember(prior);
            }
            Expect(HostOwnerIntentDisposition.Prepare, capacity.Decide(active, null));
            capacity.Remember(active);
            Expect(HostOwnerIntentDisposition.KeepGrant, capacity.Decide(active, active));
            for (int i = 0; i < 20; i++)
            {
                var overflow = (Actor: 1L, Request: Guid.NewGuid());
                Expect(HostOwnerIntentDisposition.Refuse, capacity.Decide(overflow, active));
                capacity.Remember(overflow);
            }
            capacity.Remember(active); // The terminal acknowledgement has the same identity as the grant.
            Check(capacity.Count == 256, "Overflow or retirement exceeded the hard bound.");
            Expect(HostOwnerIntentDisposition.Refuse, capacity.Decide(active, null));
            Expect(HostOwnerIntentDisposition.Refuse, capacity.Decide((3, Guid.NewGuid()), null));
            capacity.Reset();
            Check(capacity.Count == 0, "World reset did not reset the capacity.");
            Expect(HostOwnerIntentDisposition.Prepare, capacity.Decide(active, null));
        });

        test("host intent capacity counts each actor request once including loaded grants and terminal retries", () =>
        {
            var capacity = new HostOwnerIntentCapacity();
            var active = (Actor: 1L, Request: Guid.NewGuid());
            var differentActor = (Actor: 2L, Request: active.Request);
            Expect(HostOwnerIntentDisposition.KeepGrant, capacity.Decide(active, active));
            Check(capacity.Count == 1, "The loaded native grant needs one slot before any refusal.");
            Expect(HostOwnerIntentDisposition.Refuse, capacity.Decide(differentActor, active));
            capacity.Remember(differentActor);
            for (int i = 0; i < 20; i++)
            {
                capacity.Remember(active);
                capacity.Remember(differentActor);
                Expect(HostOwnerIntentDisposition.Refuse, capacity.Decide(active, null));
            }
            Check(capacity.Count == 2, "Terminal retries must not count as both completed and refused.");
            Expect(HostOwnerIntentDisposition.Prepare, capacity.Decide((3, Guid.NewGuid()), null));
        });
    }

    private static void Expect(HostOwnerIntentDisposition expected, HostOwnerIntentDisposition actual)
        => Check(expected == actual, $"Expected {expected}, got {actual}.");

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
