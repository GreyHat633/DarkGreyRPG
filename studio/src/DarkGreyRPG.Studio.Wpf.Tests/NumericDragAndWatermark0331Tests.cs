using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Input;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class NumericDragAndWatermark0331Tests
{
    [STATestMethod]
    public void EmptyWatermarkIsAnAdornerAndNeverReplacesText()
    {
        var box = new TextBox { Width = 240, Height = 32, Text = "" };
        using var host = Host(box);
        TextInputWatermark.SetText(box, "玩家将在游戏中看到的目标文字。");

        var adorners = AdornerLayer.GetAdornerLayer(box)!.GetAdorners(box);
        Assert.IsNotNull(adorners);
        Assert.AreEqual(1, adorners!.Length);
        Assert.AreEqual(Visibility.Visible, adorners[0].Visibility);
        Assert.AreEqual("", box.Text);

        box.Text = "作者自己的内容";
        Assert.AreEqual(Visibility.Collapsed, adorners[0].Visibility);
        Assert.AreEqual("作者自己的内容", box.Text);

        box.Text = "";
        Assert.AreEqual(Visibility.Visible, adorners[0].Visibility);
        TextInputWatermark.SetText(box, "");
        Assert.IsNull(AdornerLayer.GetAdornerLayer(box)!.GetAdorners(box));
    }

    [STATestMethod]
    public void WatermarkTracksCollapsedAncestorAndStaysInsideTextBoxBounds()
    {
        var box = new TextBox { Width = 120, Height = 32 };
        var panel = new StackPanel(); panel.Children.Add(box);
        using var host = Host(panel);
        TextInputWatermark.SetText(box, "这是很长的提示文字，不能越过输入框边界。");
        panel.UpdateLayout();
        var adorner = AdornerLayer.GetAdornerLayer(box)!.GetAdorners(box)!.Single();
        Assert.AreEqual(Visibility.Visible, adorner.Visibility);
        Assert.IsTrue(adorner.IsClipEnabled);
        Assert.IsTrue(adorner.RenderSize.Width <= box.RenderSize.Width);
        panel.Visibility = Visibility.Collapsed;
        Assert.AreEqual(Visibility.Collapsed, adorner.Visibility);
        panel.Visibility = Visibility.Visible;
        Assert.AreEqual(Visibility.Visible, adorner.Visibility);
        Assert.AreEqual("", box.Text);
    }

    [STATestMethod]
    public void InvalidAndEmptyDraftsAreUntouchedByNumericPointerPreview()
    {
        var empty = new TextBox { Text = "" };
        NumericDrag.SetStep(empty, 1);
        var emptyArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent };
        empty.RaiseEvent(emptyArgs);
        Assert.AreEqual("", empty.Text);

        var invalid = new TextBox { Text = "-" };
        NumericDrag.SetStep(invalid, 1);
        var invalidArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent };
        invalid.RaiseEvent(invalidArgs);
        Assert.AreEqual("-", invalid.Text);
    }

    [STATestMethod]
    public void CommittedEventBubblesWithBeforeAndAfterText()
    {
        var root = new Grid();
        var box = new TextBox { Text = "1" };
        root.Children.Add(box);
        string? before = null;
        string? after = null;
        root.AddHandler(NumericDrag.CommittedEvent, new RoutedEventHandler((_, args) =>
        {
            var committed = (NumericDrag.CommittedEventArgs)args;
            before = committed.BeforeText;
            after = committed.ValueText;
        }));

        box.RaiseEvent(new NumericDrag.CommittedEventArgs("1", "3"));

        Assert.AreEqual("1", before);
        Assert.AreEqual("3", after);
    }

    [STATestMethod]
    public void PropertyChangedBindingReceivesOneSourceEditAfterGesture()
    {
        var model = new Probe("1");
        var box = PrepareBoundBox(model, UpdateSourceTrigger.PropertyChanged);
        PrepareDraggingState(box, "1", "3");

        Finish(box, commit: true);

        Assert.AreEqual("3", model.Value);
        Assert.AreEqual(1, model.SetCount);
    }

    [STATestMethod]
    public void LostFocusBindingReceivesOneSourceEditAfterGesture()
    {
        var model = new Probe("1");
        var box = PrepareBoundBox(model, UpdateSourceTrigger.LostFocus);
        PrepareDraggingState(box, "1", "3");

        Finish(box, commit: true);

        Assert.AreEqual("3", model.Value);
        Assert.AreEqual(1, model.SetCount);
    }

    [STATestMethod]
    public void CancellationRestoresBoundTextWithoutSourceEdit()
    {
        var model = new Probe("1");
        var box = PrepareBoundBox(model, UpdateSourceTrigger.PropertyChanged);
        PrepareDraggingState(box, "1", "3");

        Finish(box, commit: false);

        Assert.AreEqual("1", model.Value);
        Assert.AreEqual("1", box.Text);
        Assert.AreEqual(0, model.SetCount);
    }

    private static TextBox PrepareBoundBox(Probe model, UpdateSourceTrigger trigger)
    {
        var box = new TextBox();
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(Probe.Value))
        {
            Source = model, Mode = BindingMode.TwoWay, UpdateSourceTrigger = trigger
        });
        box.UpdateLayout();
        NumericDrag.SetStep(box, 1);
        return box;
    }

    private static void PrepareDraggingState(TextBox box, string before, string value)
    {
        var stateProperty = (DependencyProperty)typeof(NumericDrag)
            .GetField("StateProperty", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var state = box.GetValue(stateProperty)!;
        var stateType = state.GetType();
        stateType.GetField("Before")!.SetValue(state, before);
        stateType.GetField("Binding")!.SetValue(state, BindingOperations.GetBindingBase(box, TextBox.TextProperty));
        stateType.GetField("Pending")!.SetValue(state, true);
        stateType.GetField("Dragging")!.SetValue(state, true);
        BindingOperations.ClearBinding(box, TextBox.TextProperty);
        box.Text = value;
    }

    private static void Finish(TextBox box, bool commit)
    {
        var stateProperty = (DependencyProperty)typeof(NumericDrag)
            .GetField("StateProperty", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var state = box.GetValue(stateProperty)!;
        typeof(NumericDrag).GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [state, commit]);
    }

    private static HwndSource Host(FrameworkElement child)
    {
        var decorator = new AdornerDecorator { Width = 300, Height = 50, Child = child };
        var source = new HwndSource(new HwndSourceParameters("NumericDragWatermarkTests") { Width = 300, Height = 50 });
        source.RootVisual = decorator;
        decorator.Measure(new Size(300, 50));
        decorator.Arrange(new Rect(0, 0, 300, 50));
        decorator.UpdateLayout();
        return source;
    }

    private sealed class Probe(string value) : INotifyPropertyChanged
    {
        private string _value = value;
        public string Value
        {
            get => _value;
            set { SetCount++; _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
        }
        public int SetCount { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
