using System.Collections;
using System.Windows;
using System.Windows.Controls;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Keeps the action identity independent of selection resets during mode changes.</summary>
public sealed class ActionTypeSelector : ComboBox
{
    public static readonly DependencyProperty ActionTypeProperty = DependencyProperty.Register(
        nameof(ActionType), typeof(string), typeof(ActionTypeSelector),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (sender, _) => ((ActionTypeSelector)sender).SynchronizeSelection()));

    public string? ActionType
    {
        get => (string?)GetValue(ActionTypeProperty);
        set => SetValue(ActionTypeProperty, value);
    }

    private bool _synchronizing;

    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
    {
        _synchronizing = true;
        try { base.OnItemsSourceChanged(oldValue, newValue); }
        finally { _synchronizing = false; }
        SynchronizeSelection();
    }

    private void SynchronizeSelection()
    {
        if (_synchronizing) return;
        _synchronizing = true;
        try { SetCurrentValue(SelectedItemProperty, Items.OfType<CanonicalStoryActionTypeOption>().FirstOrDefault(o => o.Value == ActionType)); }
        finally { _synchronizing = false; }
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (!_synchronizing && SelectedItem is CanonicalStoryActionTypeOption option)
            SetCurrentValue(ActionTypeProperty, option.Value);
    }
}
