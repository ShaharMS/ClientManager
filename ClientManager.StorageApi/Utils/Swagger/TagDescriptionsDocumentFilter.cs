using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClientManager.StorageApi.Utils.Swagger;

/// <summary>
/// Adds human-readable descriptions to the storage API's Swagger tags.
/// </summary>
public class TagDescriptionsDocumentFilter : IDocumentFilter
{
    private static readonly Dictionary<string, string> TagDescriptions = new()
    {
        ["Client Configurations"] = "Internal CRUD and nested-configuration endpoints for client documents.",
        ["Services"] = "Internal CRUD and search endpoints for service definitions.",
        ["Resource Pools"] = "Internal CRUD and search endpoints for resource-pool definitions.",
        ["Global Rate Limits"] = "Internal CRUD and search endpoints for global rate-limit definitions.",
        ["Runtime Operations"] = "Internal access-check and resource-allocation endpoints that own runtime state close to storage.",
        ["Statistics Reads"] = "Placeholder internal endpoints for storage-side statistics and read models.",
        ["Status"] = "Internal health and readiness endpoints for the storage API host."
    };

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var usedTags = new HashSet<string>();
        foreach (var path in swaggerDoc.Paths)
        {
            var pathItem = path.Value;
            if (pathItem is null)
            {
                continue;
            }

            var operations = pathItem.Operations;
            if (operations is null)
            {
                continue;
            }

            foreach (var operation in operations)
            {
                var tags = operation.Value?.Tags;
                if (tags is null)
                {
                    continue;
                }

                foreach (var tag in tags)
                {
                    if (tag.Name is not null)
                    {
                        usedTags.Add(tag.Name);
                    }
                }
            }
        }

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