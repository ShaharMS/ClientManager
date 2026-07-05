using System.Text.Json;
using System.Text.Json.Nodes;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Utils;

/// <summary>
/// Merges partial JSON patches into catalog entities using read-modify-write semantics.
/// </summary>
public static class EntityPatchMerger
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> NonPatchableFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt"
    };

    private static readonly HashSet<string> ClientDictionaryFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "services",
        "resourcePools"
    };

    private static readonly HashSet<string> ClientDeepObjectFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "globalRateLimit"
    };

    /// <summary>
    /// Applies <paramref name="patch"/> onto <paramref name="existing"/> and returns the merged entity.
    /// </summary>
    public static TEntity Merge<TEntity>(TEntity existing, JsonElement patch) where TEntity : class
    {
        if (patch.ValueKind != JsonValueKind.Object)
        {
            throw new BadRequestException("Each patch item must be a JSON object.");
        }

        var patchObject = JsonObject.Create(patch)
            ?? throw new BadRequestException("Each patch item must be a JSON object.");

        if (!patchObject.TryGetPropertyValue("id", out var idNode)
            || idNode is null
            || idNode.GetValueKind() != JsonValueKind.String
            || string.IsNullOrWhiteSpace(idNode.GetValue<string>()))
        {
            throw new BadRequestException("Each patch item must include a non-empty \"id\" property.");
        }

        foreach (var field in NonPatchableFields)
        {
            if (patchObject.ContainsKey(field))
            {
                throw new BadRequestException($"Property \"{field}\" cannot be patched.");
            }
        }

        var allowed = GetAllowedPropertyNames(typeof(TEntity));
        foreach (var property in patchObject)
        {
            if (property.Key.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!allowed.Contains(property.Key))
            {
                throw new BadRequestException($"Unknown property \"{property.Key}\".");
            }
        }

        var merged = JsonSerializer.SerializeToNode(existing, JsonOptions) as JsonObject
            ?? throw new InvalidOperationException($"Could not serialize {typeof(TEntity).Name} to JSON.");

        var deepDictionaryMerge = typeof(TEntity) == typeof(ClientConfiguration);
        ApplyPatch(merged, patchObject, deepDictionaryMerge);

        return JsonSerializer.Deserialize<TEntity>(merged, JsonOptions)
            ?? throw new InvalidOperationException($"Could not deserialize merged {typeof(TEntity).Name}.");
    }

    private static HashSet<string> GetAllowedPropertyNames(Type entityType)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in entityType.GetProperties())
        {
            if (NonPatchableFields.Contains(ToJsonName(property.Name)))
            {
                continue;
            }

            names.Add(ToJsonName(property.Name));
        }

        return names;
    }

    private static string ToJsonName(string propertyName) =>
        JsonNamingPolicy.CamelCase.ConvertName(propertyName);

    private static void ApplyPatch(JsonObject target, JsonObject patch, bool deepDictionaryMerge)
    {
        foreach (var (key, value) in patch)
        {
            if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (deepDictionaryMerge && ClientDictionaryFields.Contains(key))
            {
                MergeDictionaryField(target, key, value);
                continue;
            }

            if (deepDictionaryMerge && ClientDeepObjectFields.Contains(key))
            {
                MergeObjectField(target, key, value);
                continue;
            }

            target[key] = value?.DeepClone();
        }
    }

    private static void MergeDictionaryField(JsonObject target, string key, JsonNode? patchValue)
    {
        if (patchValue is not JsonObject patchDictionary)
        {
            target[key] = patchValue?.DeepClone();
            return;
        }

        var existingDictionary = target[key] as JsonObject ?? [];
        foreach (var (entryKey, entryValue) in patchDictionary)
        {
            if (entryValue is JsonObject entryPatch)
            {
                var existingEntry = existingDictionary[entryKey] as JsonObject ?? [];
                foreach (var (fieldKey, fieldValue) in entryPatch)
                {
                    existingEntry[fieldKey] = fieldValue?.DeepClone();
                }

                existingDictionary[entryKey] = existingEntry;
            }
            else
            {
                existingDictionary[entryKey] = entryValue?.DeepClone();
            }
        }

        target[key] = existingDictionary;
    }

    private static void MergeObjectField(JsonObject target, string key, JsonNode? patchValue)
    {
        if (patchValue is not JsonObject patchObject)
        {
            target[key] = patchValue?.DeepClone();
            return;
        }

        var existingObject = target[key] as JsonObject ?? [];
        foreach (var (fieldKey, fieldValue) in patchObject)
        {
            existingObject[fieldKey] = fieldValue?.DeepClone();
        }

        target[key] = existingObject;
    }
}
