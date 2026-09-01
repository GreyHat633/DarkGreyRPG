namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Stable canonical identity for the Session-only Flow Judgment node.</summary>
public static class FlowJudgmentSchema
{
    public const string NodeType = "flow_judgment";
    public const string FlowInputPortId = "flow_in";
    public const string FlowOutputPortId = "flow_out";
    public const string ExecutedPortId = "executed";
    public const string ExecutedDisplayName = "执行状态";
}
