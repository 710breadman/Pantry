using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pantry.Catalog;

public static class RecipeJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            WriteIndented = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DateOnlyJsonConverter());

        return options;
    }
}

