using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prowo.WebAsm.Shared;

public sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return TimeOnly.Parse(value!);
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        var timeString = value.ToString("HH:mm:ss.fff");
        writer.WriteStringValue(timeString);
    }
}