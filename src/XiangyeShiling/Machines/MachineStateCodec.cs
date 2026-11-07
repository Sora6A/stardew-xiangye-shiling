using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiangyeShiling.Machines;

/// <summary>A versioned single-value save format. Invalid/unknown data is left intact by the caller.</summary>
public static class MachineStateCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    /// <summary>只序列化满足当前 schema 不变量的状态；不负责写入游戏设备或保存文件。</summary>
    public static string Write(MachineState state)
    {
        if (!IsValid(state))
            throw new ArgumentException("Invalid machine state; refusing to persist it.", nameof(state));
        return JsonSerializer.Serialize(state, Options);
    }

    /// <summary>严格读取版本 1 状态；格式、字段或不变量不符时返回 false 且 state 为 null。</summary>
    /// <remarks>
    /// 调用者必须保留原 JSON 并阻止操作，不能把失败解释为“空设备”。
    /// 未来新增字段要先定义版本迁移，不能靠反序列化默认值猜测旧存档含义。
    /// </remarks>
    public static bool TryRead(string? json, out MachineState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            // Reject duplicate fields instead of silently allowing last-value-wins save data.
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (HasDuplicateProperties(root) || !HasShape(root,
                "SchemaVersion", "InstanceId", "Kind", "Revision", "Batch", "LastRequestId", "LastActorId"))
                return false;
            JsonElement batch = root.GetProperty("Batch");
            if (batch.ValueKind != JsonValueKind.Null)
            {
                if (!HasShape(batch, "Id", "RecipeId", "Inputs", "Output", "StartedAt", "ReadyAt")
                    || !HasShape(batch.GetProperty("Output"), "ItemId", "Quantity", "Quality", "MaxStack")
                    || batch.GetProperty("Inputs").ValueKind != JsonValueKind.Array)
                    return false;
                foreach (JsonElement input in batch.GetProperty("Inputs").EnumerateArray())
                    if (!HasShape(input, "Slot", "ItemId", "Quantity", "Quality"))
                        return false;
            }
            MachineState? candidate = JsonSerializer.Deserialize<MachineState>(json, Options);
            if (!IsValid(candidate))
                return false;
            state = candidate;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasShape(JsonElement element, params string[] fields) =>
        element.ValueKind == JsonValueKind.Object && element.EnumerateObject().Count() == fields.Length
        && fields.All(name => element.TryGetProperty(name, out _));

    /// <summary>校验内存状态；与 JSON 字段完整性检查分开，供规则入口拒绝无效快照。</summary>
    public static bool IsValid(MachineState? state)
    {
        if (state is null || state.SchemaVersion != MachineState.CurrentSchema || state.InstanceId == Guid.Empty
            || !Enum.IsDefined(typeof(MachineKind), state.Kind) || state.Revision < 0
            || state.LastRequestId == Guid.Empty || state.LastActorId == 0
            || (state.LastRequestId.HasValue != state.LastActorId.HasValue)
            // 新设备只有 revision=0 且没有历史请求；交易后不得清掉历史再变回新设备。
            || (state.Revision == 0) != !state.LastRequestId.HasValue)
            return false;
        if (state.Batch is not Batch batch)
            return true;
        return state.Revision > 0 && batch.Id != Guid.Empty && batch.Id == state.LastRequestId
            && !string.IsNullOrWhiteSpace(batch.RecipeId) && MachineRules.ValidProduct(batch.Output)
            && batch.StartedAt >= 0 && batch.ReadyAt > batch.StartedAt
            && batch.Inputs is { Count: > 0 }
            && batch.Inputs.All(i => i is not null && i.Slot >= 0 && i.Quantity > 0
                && MachineRules.ValidItemId(i.ItemId) && MachineRules.ValidQuality(i.Quality))
            && batch.Inputs.Select(i => i.Slot).Distinct().Count() == batch.Inputs.Count;
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value))
                    return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
                if (HasDuplicateProperties(item))
                    return true;
        }
        return false;
    }
}
