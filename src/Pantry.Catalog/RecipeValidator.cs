using NJsonSchema;

namespace Pantry.Catalog;

public sealed class RecipeValidator
{
    public async Task ValidateAsync(string recipeJson, string schemaPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);

        var schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken).ConfigureAwait(false);
        var schema = await JsonSchema.FromJsonAsync(schemaJson, cancellationToken).ConfigureAwait(false);
        var errors = schema.Validate(recipeJson);

        if (errors.Count == 0)
        {
            return;
        }

        var details = string.Join("; ", errors.Select(error => $"{error.Path}: {error.Kind}"));
        throw new RecipeValidationException($"Recipe failed JSON Schema validation: {details}");
    }
}

