using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Full-width editor header with a fixed chevron slot.</summary>
public sealed class FoldHeader : ToggleButton
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(FoldHeader),
        new PropertyMetadata("", (d, e) => { ((FoldHeader)d)._title.Text = (string)e.NewValue; System.Windows.Automation.AutomationProperties.SetName(d, (string)e.NewValue); }));
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    private readonly TextBlock _title = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _arrow = new() { Width = 16, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Text = "⌄" };
    public FoldHeader()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Padding = new Thickness(6, 5, 6, 5);
        Background = System.Windows.Media.Brushes.Transparent;
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        var surface = new FrameworkElementFactory(typeof(Border), "Surface");
        surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        surface.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        surface.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentProperty));
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        surface.AppendChild(presenter);
        var template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = surface };
        foreach (var property in new[] { IsMouseOverProperty, IsKeyboardFocusedProperty })
        {
            var trigger = new Trigger { Property = property, Value = true };
            trigger.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("CardBackgroundFillColorDefaultBrush"), "Surface"));
            template.Triggers.Add(trigger);
        }
        Template = template;
        var content = new DockPanel(); DockPanel.SetDock(_arrow, Dock.Right); content.Children.Add(_arrow); content.Children.Add(_title); Content = content;
        Checked += (_, _) => _arrow.Text = "⌃";
        Unchecked += (_, _) => _arrow.Text = "⌄";
    }
}
