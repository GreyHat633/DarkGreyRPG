using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels;

/// <summary>Chinese author-facing presentation for stable Core validation codes.</summary>
public static class ValidationIssuePresentation
{
    public static string Format(ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return Format(issue.Code, issue.Message);
    }

    public static string Format(string? code, string? technicalDetail)
    {
        var stableCode = string.IsNullOrWhiteSpace(code) ? "unknown" : code;
        var message = stableCode switch
        {
            "graph.connection.logic.input.multiple_sources" => "一个逻辑输入只能有一个来源。",
            "graph.session.start.logic_output.legacy" => "此 Session Start 使用旧版逻辑输出；可以继续读取，但新建内容只使用流程输出。",
            _ when stableCode.StartsWith("graph.connection.", StringComparison.Ordinal) => "无法建立这条节点连接，请检查端口方向和类型。",
            _ when stableCode.StartsWith("graph.dynamic_port.", StringComparison.Ordinal) => "无法更新节点的动态端口。",
            _ when stableCode.StartsWith("graph.node.create.", StringComparison.Ordinal) => "无法创建该节点。",
            _ when stableCode.StartsWith("graph.story.start.", StringComparison.Ordinal) => "Story 开始节点的启动方式配置有误。",
            _ when stableCode.StartsWith("graph.objective.", StringComparison.Ordinal) => "任务目标的类型、目标资源或数量配置有误。",
            _ when stableCode.StartsWith("graph.story.action.", StringComparison.Ordinal) => "Story 动作的参数配置有误。",
            _ when stableCode.StartsWith("graph.aggregate.", StringComparison.Ordinal) => "Session / Task 聚合节点的资源映射有误。",
            _ when IsResourceCode(stableCode) => "Story 资源缺失、重复或配置无效。",
            _ when IsSaveCode(stableCode) => "保存失败，请检查资源内容和项目状态。",
            _ when IsMigrationCode(stableCode) => "项目迁移未完成，请先处理迁移问题。",
            _ => string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(message))
        {
            var detail = string.IsNullOrWhiteSpace(technicalDetail)
                ? string.Empty
                : $"{Environment.NewLine}技术详情：{technicalDetail}";
            return $"{message}{Environment.NewLine}[{stableCode}]{detail}";
        }
        return $"操作失败（错误代码：{stableCode}）{Environment.NewLine}技术详情：{technicalDetail}";
    }

    private static bool IsResourceCode(string code)
        => code.StartsWith("story.", StringComparison.Ordinal)
            || code.StartsWith("actor.", StringComparison.Ordinal)
            || code.StartsWith("item.", StringComparison.Ordinal)
            || code.StartsWith("graph.resource.", StringComparison.Ordinal)
            || code.Contains("resource", StringComparison.Ordinal);

    private static bool IsSaveCode(string code)
        => code.StartsWith("save.", StringComparison.Ordinal)
            || code.Contains("persistence", StringComparison.Ordinal);

    private static bool IsMigrationCode(string code)
        => code.StartsWith("migration.", StringComparison.Ordinal)
            || code.Contains("migration", StringComparison.Ordinal);
}
