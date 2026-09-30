using System.Globalization;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class CanonicalNodeInspectorViewModel
{
    private StoryRepeatCondition _repeatCondition = new();
    private string _repeatValue = "24", _repeatTime = "12:00", _repeatMonth = "1", _repeatDay = "1";
    private string? _repeatProjection;
    public IReadOnlyList<CanonicalStoryStartRepeatPolicyOption> RepeatModes { get; } = [new("once", "不可重复"), new("none", "无条件重复"), new("cooldown", "冷却重复"), new("scheduled", "定时重复")];
    public IReadOnlyList<CanonicalStoryStartRepeatPolicyOption> RepeatUnits { get; } = [new("seconds", "秒"), new("minutes", "分钟"), new("hours", "小时")];
    public IReadOnlyList<CanonicalStoryStartRepeatPolicyOption> RepeatPeriods { get; } = [new("daily", "每日"), new("weekly", "每周"), new("yearly", "每年")];
    public IReadOnlyList<RepeatWeekdayOption> RepeatWeekdays { get; } = Enumerable.Range(1, 7).Select(i => new RepeatWeekdayOption(i, "星期" + "一二三四五六日"[i - 1])).ToArray();
    public string RepeatMode
    {
        get => IsRepeatable ? _repeatCondition.Type : "once";
        set
        {
            if (_isProjectingCanonicalChange || value == RepeatMode || !RepeatModes.Any(mode => mode.Value == value)) return;
            var condition = value == "once" ? _repeatCondition : _repeatCondition with { Type = value };
            if (condition.Error is not null) condition = new StoryRepeatCondition { Type = value == "once" ? "none" : value };
            _host.SetStoryStartRepeatConfiguration(NodeId, value == "once" ? "once" : "repeatable", condition.ToJson());
        }
    }
    public string RepeatUnit { get => _repeatCondition.Unit; set { if (_isProjectingCanonicalChange || value == RepeatUnit) return; _repeatCondition = _repeatCondition with { Unit = value }; CommitRepeat(); } }
    public string RepeatPeriod { get => _repeatCondition.Period; set { if (_isProjectingCanonicalChange || value == RepeatPeriod) return; _repeatCondition = _repeatCondition with { Period = value }; CommitRepeat(); } }
    public int RepeatWeekday { get => _repeatCondition.Weekday; set { if (_isProjectingCanonicalChange || value == RepeatWeekday) return; _repeatCondition = _repeatCondition with { Weekday = value }; CommitRepeat(); } }
    public string RepeatValue { get => _repeatValue; set { if (_isProjectingCanonicalChange || value == _repeatValue) return; _repeatValue = value; CommitRepeat(); } }
    public string RepeatTime { get => _repeatTime; set { if (_isProjectingCanonicalChange || value == _repeatTime) return; _repeatTime = value; CommitRepeat(); } }
    public string RepeatMonth { get => _repeatMonth; set { if (_isProjectingCanonicalChange || value == _repeatMonth) return; _repeatMonth = value; CommitRepeat(); } }
    public string RepeatDay { get => _repeatDay; set { if (_isProjectingCanonicalChange || value == _repeatDay) return; _repeatDay = value; CommitRepeat(); } }
    public bool HasRepeatDetails => IsRepeatCooldown || IsRepeatScheduled;
    public bool IsRepeatCooldown => RepeatMode == "cooldown";
    public bool IsRepeatScheduled => RepeatMode == "scheduled";
    public bool IsRepeatWeekly => IsRepeatScheduled && RepeatPeriod == "weekly";
    public bool IsRepeatYearly => IsRepeatScheduled && RepeatPeriod == "yearly";
    public string RepeatError { get; private set; } = "";
    public string RepeatSummary => !IsRepeatable ? "仅一次" : RepeatError.Length > 0 ? "配置有误，请展开检查"
        : RepeatMode == "none" ? "无条件" : IsRepeatCooldown ? $"冷却 {RepeatValue} {RepeatUnits.FirstOrDefault(u => u.Value == RepeatUnit)?.DisplayName}"
        : IsRepeatWeekly ? $"每{RepeatWeekdays.FirstOrDefault(d => d.Value == RepeatWeekday)?.DisplayName} {RepeatTime}"
        : IsRepeatYearly ? $"每年 {RepeatMonth} 月 {RepeatDay} 日 {RepeatTime}" : $"每日 {RepeatTime}";
    private void CommitRepeat()
    {
        if (_disposed || !IsStoryStart) return;
        long.TryParse(_repeatValue, NumberStyles.None, CultureInfo.InvariantCulture, out var value);
        int.TryParse(_repeatMonth, NumberStyles.None, CultureInfo.InvariantCulture, out var month);
        int.TryParse(_repeatDay, NumberStyles.None, CultureInfo.InvariantCulture, out var day);
        _repeatCondition = _repeatCondition with { Value = value, Month = month, Day = day, Time = _repeatTime };
        RepeatError = _repeatCondition.Error ?? "";
        if (RepeatError.Length == 0 && !_host.SetNodeProperty(NodeId, "repeat_condition", _repeatCondition.ToJson()))
            RepeatError = "未能保存重复条件，请检查资源是否可编辑。";
        NotifyRepeat();
    }
    private void RefreshRepeat(GraphEditorNodeViewModel node)
    {
        var json = node.Properties.TryGetValue("repeat_condition", out var data) ? data.GetRawText() : "";
        if (_repeatProjection != json)
        {
            _repeatProjection = json;
            try { _repeatCondition = json.Length == 0 ? new() : StoryRepeatCondition.Parse(data); RepeatError = ""; }
            catch (Exception error) when (error is FormatException or InvalidOperationException or KeyNotFoundException or OverflowException) { RepeatError = error.Message; }
            _repeatValue = _repeatCondition.Value.ToString(CultureInfo.InvariantCulture);
            _repeatTime = _repeatCondition.Time;
            _repeatMonth = _repeatCondition.Month.ToString(CultureInfo.InvariantCulture);
            _repeatDay = _repeatCondition.Day.ToString(CultureInfo.InvariantCulture);
        }
        NotifyRepeat();
    }
    private void NotifyRepeat()
    {
        foreach (var name in new[] { nameof(RepeatMode), nameof(RepeatUnit), nameof(RepeatPeriod), nameof(RepeatWeekday), nameof(RepeatValue), nameof(RepeatTime), nameof(RepeatMonth), nameof(RepeatDay), nameof(HasRepeatDetails), nameof(IsRepeatCooldown), nameof(IsRepeatScheduled), nameof(IsRepeatWeekly), nameof(IsRepeatYearly), nameof(RepeatError), nameof(RepeatSummary) }) OnPropertyChanged(name);
    }
}

public sealed record RepeatWeekdayOption(int Value, string DisplayName);
