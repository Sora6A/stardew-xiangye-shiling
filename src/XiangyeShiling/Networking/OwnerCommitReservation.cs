using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using XiangyeShiling.Machines;

namespace XiangyeShiling.Networking;

/// <summary>
/// A host-authored plan for the one outstanding owner commit in a world. The caller must establish the
/// live host, owner and target before preparing this record. Constructing or loading it authorizes no write.
/// </summary>
internal sealed class OwnerCommitReservation
{
    internal const int SchemaVersion = 1;
    internal const int MaxCapacity = 128;
    private const int MaxMachineUtf8Bytes = 16 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public ulong WorldId { get; }
    public long HostId { get; }
    public Guid GrantId { get; }
    public MachineRequestEnvelope Request { get; }
    public string RequestRaw { get; }
    public string BeforeRaw { get; }
    public string AfterRaw { get; }
    public OwnerInventoryWriteSet WriteSet { get; }
    public OwnerReservationStatus Status { get; }
    public string PlanKey { get; }
    public string OwnerJournal => $"{GrantId:D}:{this.PlanKey}";

    private OwnerCommitReservation(ulong worldId, long hostId, Guid grantId, MachineRequestEnvelope request,
        string requestRaw, string beforeRaw, string afterRaw, OwnerInventoryWriteSet writeSet, OwnerReservationStatus status)
    {
        this.WorldId = worldId;
        this.HostId = hostId;
        this.GrantId = grantId;
        this.Request = request;
        this.RequestRaw = requestRaw;
        this.BeforeRaw = beforeRaw;
        this.AfterRaw = afterRaw;
        this.WriteSet = writeSet;
        this.Status = status;
        this.PlanKey = this.ComputePlanKey();
    }

    /// <summary>Bind exact raw strings, without normalizing existing machine data or inferring an inventory mutation.</summary>
    public static bool TryCreate(ulong worldId, long hostId, Guid grantId, string? requestRaw, string? beforeRaw,
        string? afterRaw, OwnerInventoryWriteSet? writeSet, [NotNullWhen(true)] out OwnerCommitReservation? reservation)
    {
        reservation = null;
        if (worldId == 0 || hostId == 0 || grantId == Guid.Empty || writeSet is null || writeSet.Capacity > MaxCapacity
            || !MachineRequestCodec.TryRead(requestRaw, out var request) || request!.ActorId == hostId
            || !TryReadMachine(beforeRaw, out var before) || !TryReadMachine(afterRaw, out var after)
            || before.InstanceId != request.InstanceId || after.InstanceId != request.InstanceId || before.Kind != after.Kind
            || before.Revision != request.ExpectedRevision || before.Revision == long.MaxValue
            || after.Revision != before.Revision + 1 || before.LastRequestId == request.RequestId
            || after.LastRequestId != request.RequestId || after.LastActorId != request.ActorId)
            return false;

        bool matchingAction = request.Action == MachineRequestAction.Start
            ? before.Batch is null && after.Batch is { } batch && batch.RecipeId == request.RecipeId
                && batch.Inputs.Select(input => input.Slot).OrderBy(slot => slot)
                    .SequenceEqual(writeSet.Changes.Select(change => change.Index).OrderBy(slot => slot))
                && writeSet.Changes.All(change => change.Before is not null)
            : before.Batch is not null && after.Batch is null && writeSet.Changes.All(change => change.After is not null);
        if (!matchingAction) return false;

        reservation = new OwnerCommitReservation(worldId, hostId, grantId, request, requestRaw!, beforeRaw!, afterRaw!,
            writeSet, OwnerReservationStatus.Reserved);
        return true;
    }

    /// <summary>The caller must first reconcile live evidence. Terminal status never changes the authorized plan.</summary>
    public OwnerCommitReservation WithTerminalStatus(OwnerReservationStatus status)
    {
        if (this.Status != OwnerReservationStatus.Reserved
            || status is not (OwnerReservationStatus.Applied or OwnerReservationStatus.Cancelled))
            throw new InvalidOperationException("A reservation can only transition from Reserved to a terminal status.");
        return new OwnerCommitReservation(this.WorldId, this.HostId, this.GrantId, this.Request, this.RequestRaw,
            this.BeforeRaw, this.AfterRaw, this.WriteSet, status);
    }

    internal static bool IsBoundedText([NotNullWhen(true)] string? text, int maxBytes)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > maxBytes) return false;
        try { return StrictUtf8.GetByteCount(text) <= maxBytes; }
        catch (EncoderFallbackException) { return false; }
    }

    private static bool TryReadMachine(string? raw, [NotNullWhen(true)] out MachineState? state)
    {
        state = null;
        if (!IsBoundedText(raw, MaxMachineUtf8Bytes)) return false;
        try { return MachineStateCodec.TryRead(raw, out state) && state is not null; }
        // A JSON-escaped unpaired surrogate can throw when the state reader decodes a property name.
        catch (InvalidOperationException) { return false; }
    }

    private string ComputePlanKey()
    {
        // Versioned, length-prefixed UTF-8 and little-endian integers. Keep this stable across saves.
        // The status is excluded because the owner's evidence must remain valid through host recovery.
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes, StrictUtf8, leaveOpen: true);
        writer.Write("XYS/OwnerCommitReservation/1");
        writer.Write(this.WorldId);
        writer.Write(this.HostId);
        writer.Write(this.GrantId.ToString("D"));
        writer.Write(this.RequestRaw);
        writer.Write(this.BeforeRaw);
        writer.Write(this.AfterRaw);
        writer.Write(this.WriteSet.Capacity);
        writer.Write(this.WriteSet.Changes.Count);
        foreach (OwnerSlotChange change in this.WriteSet.Changes.OrderBy(change => change.Index))
        {
            writer.Write(change.Index);
            // Empty fingerprints are disallowed by the write set, so an empty string encodes a null slot.
            writer.Write(change.Before ?? "");
            writer.Write(change.After ?? "");
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();
    }
}
