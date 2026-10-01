using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

public static class ScreenMorphKeys
{
    // Legacy nodes have no known cross-node copy relationship. Their detached
    // save/clipboard upgrade is stable for this node only and never writes on read.
    public static JsonElement Upgrade(JsonElement layers, string nodeId)
    {
        var array = JsonNode.Parse(layers.GetRawText())!.AsArray();
        for (int index = 0; index < array.Count; index++)
            if (array[index] is JsonObject layer && !layer.ContainsKey("morph_key"))
                layer["morph_key"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nodeId + ":" + index))).ToLowerInvariant()[..32];
        return JsonSerializer.SerializeToElement(array);
    }
}
