using System.Windows;
using System.Windows.Controls;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Views;

public partial class NamespaceDialog : Window
{
    public NamespaceDialog(string? currentNamespace, bool firstUse)
    {
        InitializeComponent();
        NamespaceTextBox.Text = currentNamespace ?? string.Empty;
        ConfirmButton.Content = firstUse ? "下一步：创建项目" : "确定";
        if (firstUse)
        {
            FirstUseText.Text = "为新项目设置 NameSpace。每个项目独立保存；此处可沿用上次创建项目时的值。";
            FirstUseText.Visibility = Visibility.Visible;
        }

        UpdateValidation();
    }

    public string NamespaceValue => NamespaceTextBox.Text;

    private void NamespaceTextBox_OnTextChanged(object sender, TextChangedEventArgs e) => UpdateValidation();

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DgrResourceId.IsValidNamespace(NamespaceValue))
        {
            DialogResult = true;
        }
        else
        {
            UpdateValidation();
            NamespaceTextBox.Focus();
        }
    }

    private void UpdateValidation()
    {
        if (NamespaceTextBox is null || ConfirmButton is null || ValidationText is null) return;
        var valid = DgrResourceId.IsValidNamespace(NamespaceValue);
        ConfirmButton.IsEnabled = valid;
        ValidationText.Text = valid ? string.Empty : "请输入有效的 NameSpace。";
    }
}
