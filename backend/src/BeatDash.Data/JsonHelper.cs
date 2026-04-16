using System.Text.Json.Nodes;

namespace Shiron.BeatDash.Data;

public static class JsonHelper {
    public static string GetValueString(this JsonNode node, string key) {
        return node[key]?.GetValue<string>() ?? "";
    }
    public static int GetValueInt(this JsonNode node, string key) {
        return (int) (node[key]?.GetValue<decimal>() ?? 0);
    }
    public static decimal GetValueDecimal(this JsonNode node, string key) {
        return node[key]?.GetValue<decimal>() ?? 0;
    }
}
