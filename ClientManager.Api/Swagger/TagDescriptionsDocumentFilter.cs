using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClientManager.Api.Swagger;

/// <summary>
/// Adds human-readable descriptions to OpenAPI tags and removes orphaned tag entries
/// that don't match any operation.
/// </summary>
public class TagDescriptionsDocumentFilter : IDocumentFilter
{
    private static readonly Dictionary<string, string> TagDescriptions = new()
    {
        ["Access Check"] = "Operational endpoints for checking client access to services.",
        ["Client Configurations"] = "Manages client configuration documents and their nested sub-resources.",
        ["Global Rate Limits"] = "Manages system-wide catch-all rate limits for services and resource pools.",
        ["Resource Allocation"] = "Operational endpoints for acquiring and releasing resource pool slots.",
        ["Resource Pools"] = "Manages system-wide resource pool definitions.",
        ["Services"] = "Manages system-wide service definitions.",
        ["Statistics"] = "Provides human-readable statistics about system state, including client counts, service usage, and resource pool utilization."
    };

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Collect tag names actually referenced by operations
        var usedTags = new HashSet<string>();
        foreach (var path in swaggerDoc.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                foreach (var tag in operation.Value.Tags)
                {
                    usedTags.Add(tag.Name);
                }
            }
        }

        // Rebuild the top-level tags list with only used tags and their descriptions
        swaggerDoc.Tags = new HashSet<OpenApiTag>(
            usedTags
                .OrderBy(name => name)
                .Select(name => new OpenApiTag
                {
                    Name = name,
                    Description = TagDescriptions.GetValueOrDefault(name)
                }));
    }
}
