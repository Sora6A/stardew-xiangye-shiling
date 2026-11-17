using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace XiangyeShiling.Networking;

/// <summary>
/// The independent host save record format. Missing data is handled by the adapter as a legacy save.
/// A present record that cannot be read must be retained verbatim and block recovery and new grants.
/// </summary>
internal static class OwnerCommitReservationCodec
{
    // Includes JSON escaping of both bounded machine states and the bounded wire request.
    internal const int MaxUtf8Bytes = 256 * 1024;
    private static readonly string[] Fields =
    {
        "SchemaVersion", "WorldId", "HostId", "GrantId", "Status", "RequestRaw", "BeforeRaw", "AfterRaw", "Capacity", "Changes"
    };
    private static readonly string[] ChangeFields = { "Index", "Before", "After" };

    public static string Write(OwnerCommitReservation reservation)
    {
        string raw = JsonSerializer.Serialize(new
        {
            OwnerCommitReservation.SchemaVersion,
            reservation.WorldId, reservation.HostId, reservation.GrantId,
            Status = reservation.Status.ToString(),
            reservation.RequestRaw, reservation.BeforeRaw, reservation.AfterRaw,
            reservation.WriteSet.Capacity, reservation.WriteSet.Changes
        });
        if (Encoding.UTF8.GetByteCount(raw) > MaxUtf8Bytes)
            throw new ArgumentException("The owner commit reservation exceeds the save limit.", nameof(reservation));
        return raw;
    }

    public static bool TryRead(string? raw, [NotNullWhen(true)] out OwnerCommitReservation? reservation)
    {
        reservation = null;
        if (!OwnerCommitReservation.IsBoundedText(raw, MaxUtf8Bytes)) return false;
        try
        {
            using var document = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 4 });
            JsonElement root = document.RootElement;
            if (!HasExactFields(root, Fields) || root.GetProperty("SchemaVersion").GetInt32() != OwnerCommitReservation.SchemaVersion
                || !Guid.TryParseExact(root.GetProperty("GrantId").GetString(), "D", out Guid grantId))
                return false;
            OwnerReservationStatus? status = root.GetProperty("Status").GetString() switch
            {
                "Reserved" => OwnerReservationStatus.Reserved,
                "Applied" => OwnerReservationStatus.Applied,
                "Cancelled" => OwnerReservationStatus.Cancelled,
                _ => null
            };
            int capacity = root.GetProperty("Capacity").GetInt32();
            JsonElement slots = root.GetProperty("Changes");
            if (status is null || capacity is < 1 or > OwnerCommitReservation.MaxCapacity
                || slots.ValueKind != JsonValueKind.Array || slots.GetArrayLength() > capacity)
                return false;

            var changes = new OwnerSlotChange[slots.GetArrayLength()];
            int index = 0;
            foreach (JsonElement slot in slots.EnumerateArray())
            {
                if (!HasExactFields(slot, ChangeFields)) return false;
                changes[index++] = new OwnerSlotChange(slot.GetProperty("Index").GetInt32(),
                    slot.GetProperty("Before").GetString(), slot.GetProperty("After").GetString());
            }
            if (!OwnerCommitReservation.TryCreate(root.GetProperty("WorldId").GetUInt64(), root.GetProperty("HostId").GetInt64(),
                grantId, root.GetProperty("RequestRaw").GetString(), root.GetProperty("BeforeRaw").GetString(),
                root.GetProperty("AfterRaw").GetString(), OwnerInventoryWriteSet.Create(capacity, changes), out var plan))
                return false;

            reservation = status.Value == OwnerReservationStatus.Reserved ? plan : plan.WithTerminalStatus(status.Value);
            return true;
        }
        catch (JsonException) { return false; }
        // Wrong types, numeric overflow and JSON-escaped invalid Unicode all fail the entire read.
        catch (InvalidOperationException) { return false; }
        catch (FormatException) { return false; }
    }

    private static bool HasExactFields(JsonElement element, string[] fields)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
            if (!fields.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name)) return false;
        return seen.Count == fields.Length;
    }
}
