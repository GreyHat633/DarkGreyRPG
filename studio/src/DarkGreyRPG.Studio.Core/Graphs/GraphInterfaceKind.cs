using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Graphs;

/// <summary>The two connection languages supported by the canonical graph model.</summary>
[JsonConverter(typeof(GraphInterfaceKindJsonConverter))]
public enum GraphInterfaceKind
{
    Flow,
    Logic,
}

/// <summary>Convenient constants for callers that refer to the field as InterfaceKind.</summary>
public static class InterfaceKind
{
    public const GraphInterfaceKind Flow = GraphInterfaceKind.Flow;
    public const GraphInterfaceKind Logic = GraphInterfaceKind.Logic;
}

[JsonConverter(typeof(GraphPortDirectionJsonConverter))]
public enum GraphPortDirection
{
    Input,
    Output,
}

public sealed class GraphPortDirectionJsonConverter : JsonConverter<GraphPortDirection>
{
    public override GraphPortDirection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() switch
        {
            "input" => GraphPortDirection.Input,
            "output" => GraphPortDirection.Output,
            var value => throw new JsonException($"Unknown graph port direction '{value}'. Expected 'input' or 'output'."),
        };

    public override void Write(Utf8JsonWriter writer, GraphPortDirection value, JsonSerializerOptions options)
        => writer.WriteStringValue(value switch
        {
            GraphPortDirection.Input => "input",
            GraphPortDirection.Output => "output",
            _ => throw new JsonException($"Unknown graph port direction '{value}'."),
        });
}

/// <summary>JSON converter for the deliberately lowercase persisted interface kind.</summary>
public sealed class GraphInterfaceKindJsonConverter : JsonConverter<GraphInterfaceKind>
{
    public override GraphInterfaceKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "flow" => GraphInterfaceKind.Flow,
            "logic" => GraphInterfaceKind.Logic,
            _ => throw new JsonException($"Unknown graph interface_kind '{value}'. Expected 'flow' or 'logic'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, GraphInterfaceKind value, JsonSerializerOptions options)
        => writer.WriteStringValue(value switch
        {
            GraphInterfaceKind.Flow => "flow",
            GraphInterfaceKind.Logic => "logic",
            _ => throw new JsonException($"Unknown graph interface kind '{value}'."),
        });
}
