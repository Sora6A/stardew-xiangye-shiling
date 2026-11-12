using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Xna.Framework.Content;
using StardewValley;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

/// <summary>
/// Reads a native inventory slot on the game thread. The caller resolves the owning Farmer and authorized recipe.
/// A true result with a null digest is an empty slot. False leaves the slot unavailable for a write set.
/// </summary>
internal static class OwnerInventoryFingerprint
{
    private const int MaxXmlBytes = 16 * 1024;
    private static readonly Lazy<XmlSerializer> NativeObjectSerializer = new(() => new(typeof(SObject)));

    internal static bool TryReadSlot(Farmer? owner, int slot, out string? fingerprint)
    {
        fingerprint = null;
        if (owner is null || slot < 0 || slot >= owner.MaxItems || owner.Items.Count != owner.MaxItems)
            return false;
        return TryRead(owner.Items[slot], out fingerprint);
    }

    internal static bool TryRead(Item? item, out string? fingerprint)
        => TryRead(item, static (id, stack, quality) => ItemRegistry.Create(id, stack, quality, allowNull: true),
            out fingerprint);

    private static bool TryRead(Item? item, Func<string, int, int, Item?> createCanonical, out string? fingerprint)
    {
        fingerprint = null;
        if (item is null) return true;
        if (item is not SObject native || native.GetType() != typeof(SObject) || native.bigCraftable.Value
            || native.modData.Pairs.Any() || native.heldObject.Value is not null || native.Stack < 1
            || native.Quality is not (0 or 1 or 2 or 4))
            return false;

        try
        {
            string id = native.QualifiedItemId;
            if (id.Length is < 4 or > 128 || !id.StartsWith("(O)", StringComparison.Ordinal)
                || native.Stack > native.maximumStackSize()) return false;
            // ID/quantity/quality alone cannot restore preserve types, mutable native fields or other mod data.
            // A matching complete Object XML image proves this slot can be recreated by the game registry.
            Item? canonical = createCanonical(id, native.Stack, native.Quality);
            if (canonical is not SObject prototype || prototype.GetType() != typeof(SObject)
                || ReferenceEquals(native, prototype) || prototype.modData.Pairs.Any() || prototype.heldObject.Value is not null
                || prototype.QualifiedItemId != id || prototype.Stack != native.Stack || prototype.Quality != native.Quality)
                return false;

            byte[] xml = WriteNativeXml(native);
            if (!xml.AsSpan().SequenceEqual(WriteNativeXml(prototype))) return false;
            fingerprint = Convert.ToHexString(SHA256.HashData(xml)).ToLowerInvariant();
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or NotSupportedException
            or XmlException or ContentLoadException or KeyNotFoundException)
        {
            // Unknown content, unsupported native values and oversized XML cannot certify a recoverable item.
            return false;
        }
    }

    private static byte[] WriteNativeXml(SObject item)
    {
        // Fixed capacity bounds serialization even if a live object contains an unexpected native string.
        using var bytes = new MemoryStream(new byte[MaxXmlBytes], writable: true);
        bytes.SetLength(0);
        using (var writer = XmlWriter.Create(bytes, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = true,
            NewLineHandling = NewLineHandling.Entitize
        }))
            NativeObjectSerializer.Value.Serialize(writer, item);
        return bytes.ToArray();
    }
}
