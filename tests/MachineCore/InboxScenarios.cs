using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using XiangyeShiling.Networking;

internal static class InboxScenarios
{
    private sealed record Message(int Value);

    public static void Run(Action<string, Action> test)
    {
        test("inbox rejects nonpositive capacity and defaults to an empty queue", () =>
        {
            Expect<ArgumentOutOfRangeException>(() => new ProtocolInbox<Message>(0), "Zero capacity accepted.");
            Expect<ArgumentOutOfRangeException>(() => new ProtocolInbox<Message>(-1), "Negative capacity accepted.");
            Assert(new ProtocolInbox<Message>().Count == 0, "A new inbox contains invented work.");
        });
        test("inbox preserves FIFO references and rejects overflow without discarding queued work", () =>
        {
            var inbox = new ProtocolInbox<Message>(2);
            var first = new Message(1);
            var second = new Message(2);
            Assert(inbox.TryEnqueue(first) && inbox.TryEnqueue(second), "Available capacity was rejected.");
            Assert(!inbox.TryEnqueue(new Message(3)) && inbox.Count == 2, "Overflow grew or replaced the queue.");
            Assert(inbox.TryDequeue(out var a) && ReferenceEquals(a, first), "First item lost its order/reference.");
            Assert(inbox.TryDequeue(out var b) && ReferenceEquals(b, second), "Second item lost its order/reference.");
            Assert(!inbox.TryDequeue(out var empty) && empty is null && inbox.Count == 0,
                "An empty inbox returned stale work.");
        });
        test("inbox rejects null without consuming capacity or changing queued work", () =>
        {
            var inbox = new ProtocolInbox<Message>(1);
            var original = new Message(1);
            Assert(inbox.TryEnqueue(original), "Initial work was rejected.");
            Expect<ArgumentNullException>(() => inbox.TryEnqueue(null!), "Null was treated as queue overflow.");
            Assert(inbox.Count == 1 && inbox.TryDequeue(out var item) && ReferenceEquals(item, original),
                "Invalid enqueue removed or changed existing work.");
            Expect<ArgumentNullException>(() => inbox.TryEnqueue(null!), "An empty queue accepted null.");
            Assert(inbox.Count == 0, "Null consumed capacity.");
        });
        test("inbox concurrent producers cannot exceed capacity or duplicate accepted work", () =>
        {
            const int capacity = 37;
            const int producers = 8;
            const int perProducer = 100;
            var inbox = new ProtocolInbox<Message>(capacity);
            var failures = new ConcurrentQueue<Exception>();
            using var ready = new CountdownEvent(producers);
            using var start = new ManualResetEventSlim(false);
            var workers = new Thread[producers];
            int accepted = 0;
            int exceeded = 0;
            for (int i = 0; i < workers.Length; i++)
            {
                int producer = i;
                workers[i] = new Thread(() =>
                {
                    ready.Signal();
                    try
                    {
                        Assert(start.Wait(TimeSpan.FromSeconds(5)), "Concurrent producer start timed out.");
                        for (int n = 0; n < perProducer; n++)
                        {
                            if (inbox.TryEnqueue(new Message(producer * perProducer + n)))
                                Interlocked.Increment(ref accepted);
                            if (inbox.Count > capacity) Interlocked.Increment(ref exceeded);
                        }
                    }
                    catch (Exception ex) { failures.Enqueue(ex); }
                }) { IsBackground = true };
                workers[i].Start();
            }
            bool allReady = ready.Wait(TimeSpan.FromSeconds(5));
            start.Set();
            bool allJoined = true;
            foreach (Thread worker in workers)
                allJoined &= worker.Join(TimeSpan.FromSeconds(5));
            Assert(allReady && allJoined && failures.IsEmpty, "Concurrent producers failed or did not finish.");
            Assert(accepted == capacity && inbox.Count == capacity && exceeded == 0,
                "Concurrent capacity check accepted too much or rejected available space.");
            var values = new HashSet<int>();
            while (inbox.TryDequeue(out var item))
                Assert(item is not null && values.Add(item.Value), "Accepted work was duplicated or replaced with null.");
            Assert(values.Count == accepted && inbox.Count == 0, "Accepted work disappeared during dequeue.");
        });
        test("inbox rejects foreign-thread consume and clear but permits enqueue and count", () =>
        {
            var inbox = new ProtocolInbox<Message>(3);
            var original = new Message(1);
            var produced = new Message(2);
            Assert(inbox.TryEnqueue(original), "Initial work was rejected.");
            Exception? failure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    Assert(inbox.TryEnqueue(produced) && inbox.Count == 2, "Foreign producer/count was rejected.");
                    Expect<InvalidOperationException>(() => inbox.TryDequeue(out _), "Foreign consumer was allowed.");
                    Expect<InvalidOperationException>(() => inbox.Clear(), "Foreign clear was allowed.");
                }
                catch (Exception ex) { failure = ex; }
            }) { IsBackground = true };
            worker.Start();
            Assert(worker.Join(TimeSpan.FromSeconds(5)) && failure is null, "Foreign-thread contract failed.");
            Assert(inbox.Count == 2 && inbox.TryDequeue(out var a) && ReferenceEquals(a, original)
                && inbox.TryDequeue(out var b) && ReferenceEquals(b, produced),
                "Rejected consumer/clear mutated the queue.");
        });
        test("inbox clear removes old lifecycle work and permits fresh work", () =>
        {
            var inbox = new ProtocolInbox<Message>(2);
            Assert(inbox.TryEnqueue(new Message(1)) && inbox.TryEnqueue(new Message(2)), "Initial work rejected.");
            inbox.Clear();
            Assert(inbox.Count == 0 && !inbox.TryDequeue(out var old) && old is null, "Clear retained old work.");
            var fresh = new Message(3);
            Assert(inbox.TryEnqueue(fresh) && inbox.TryDequeue(out var item) && ReferenceEquals(item, fresh),
                "A cleared inbox rejected or substituted fresh work.");
            inbox.Clear();
            Assert(inbox.Count == 0, "Clearing an empty inbox changed it.");
        });
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Expect<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }
}
