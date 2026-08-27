using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DarkGreyRPG.Studio.Views;

/// <summary>
/// Creates the context menus used by the editors. The styles are application
/// resources so WPF's Fluent theme can update them when ThemeMode changes.
/// </summary>
internal static class FluentContextMenuFactory
{
    private const string MenuStyleKey = "FluentContextMenuStyle";
    private const string ItemStyleKey = "FluentContextMenuItemStyle";
    private const string CriticalItemStyleKey = "FluentContextMenuCriticalItemStyle";
    private const string SeparatorStyleKey = "FluentContextMenuSeparatorStyle";
    private const string FluentIconsFont = "Segoe Fluent Icons, Segoe MDL2 Assets";

    public static ContextMenu Create(UIElement? placementTarget = null, PlacementMode placement = PlacementMode.MousePoint)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = placementTarget,
            Placement = placement,
        };
        menu.SetResourceReference(FrameworkElement.StyleProperty, MenuStyleKey);
        return menu;
    }

    public static MenuItem CreateSubmenu(string header)
    {
        var item = CreateItem(header, action: null);
        return item;
    }

    public static MenuItem CreateItem(
        string header,
        Action? action,
        bool enabled = true,
        bool critical = false)
    {
        var item = new MenuItem
        {
            Header = header,
            IsEnabled = enabled,
        };
        item.SetResourceReference(FrameworkElement.StyleProperty, critical ? CriticalItemStyleKey : ItemStyleKey);
        AutomationProperties.SetName(item, header);
        item.Icon = CreateIcon(header);
        if (action is not null)
        {
            item.Click += (_, _) => action();
        }

        return item;
    }

    public static Separator CreateSeparator()
    {
        var separator = new Separator();
        separator.SetResourceReference(FrameworkElement.StyleProperty, SeparatorStyleKey);
        return separator;
    }

    private static TextBlock? CreateIcon(string header)
    {
        var glyph = header switch
        {
            "编辑剧情" => "\uE70F",
            "进入剧情" => "\uE8A7",
            "删除剧情" or "删除节点" or "删除连接" => "\uE74D",
            "复制" or "复制 Story ID" => "\uE8C8",
            "粘贴" => "\uE77F",
            "创建副本" => "\uE8B9",
            "自动布局" => "\uE8B5",
            "适应全部节点" => "\uE9D2",
            "实际大小" => "\uE91B",
            "重置视图" => "\uE72C",
            "添加节点" => "\uE710",
            _ when header.StartsWith("打开", StringComparison.Ordinal) => "\uE8A7",
            _ when header.StartsWith("查看", StringComparison.Ordinal) => "\uE890",
            _ when header.StartsWith("定位", StringComparison.Ordinal) => "\uE81D",
            _ when header.StartsWith("聚焦", StringComparison.Ordinal) => "\uE71B",
            _ => null,
        };

        if (glyph is null)
        {
            return null;
        }

        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily(FluentIconsFont),
            FontSize = 16,
            Width = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(icon, string.Empty);
        AutomationProperties.SetHelpText(icon, string.Empty);
        return icon;
    }
}
