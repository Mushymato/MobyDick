using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MobyDick.Model;

public sealed class StringIntListConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(List<int>);
    }

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        JToken token = JToken.Load(reader);
        return token.Type switch
        {
            JTokenType.Null => null,
            JTokenType.String => FromString(token.ToObject<string>()),
            JTokenType.Array => token.ToObject<List<int>>(),
            _ => [token.ToObject<int>()!],
        };
    }

    private static List<int>? FromString(string? strValue)
    {
        if (string.IsNullOrEmpty(strValue))
            return null;
        string[] parts = strValue.Split(',');
        List<int> result = [];
        foreach (string part in parts)
        {
            if (int.TryParse(part, out int index))
            {
                result.Add(index);
            }
        }
        return result.Count > 0 ? result : null;
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
