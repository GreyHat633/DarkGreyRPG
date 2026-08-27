using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Settings;

public sealed record StudioSettings
{
    public const int CurrentSchemaVersion = 1;
    public const double DefaultWindowWidth = 1180;
    public const double DefaultWindowHeight = 760;
    public const double DefaultResourceBrowserWidth = 260;
    public const double StoryResourceLibraryMinWidth = 220;
    public const double StoryResourceLibraryMaxWidth = 380;
    public const double DefaultStoryResourceLibraryWidth = 280;
    public const double DefaultBottomPanelHeight = 220;

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("theme")]
    public ThemePreference Theme { get; init; } = ThemePreference.System;

    [JsonPropertyName("window_width")]
    public double WindowWidth { get; init; } = DefaultWindowWidth;

    [JsonPropertyName("window_height")]
    public double WindowHeight { get; init; } = DefaultWindowHeight;

    [JsonPropertyName("window_maximized")]
    public bool WindowMaximized { get; init; }

    [JsonPropertyName("resource_browser_width")]
    public double ResourceBrowserWidth { get; init; } = DefaultResourceBrowserWidth;

    [JsonPropertyName("story_resource_library_width")]
    public double StoryResourceLibraryWidth { get; init; } = DefaultStoryResourceLibraryWidth;

    [JsonPropertyName("bottom_panel_height")]
    public double BottomPanelHeight { get; init; } = DefaultBottomPanelHeight;

    [JsonPropertyName("last_project")]
    public string? LastProject { get; init; }

    [JsonPropertyName("recent_projects")]
    public IReadOnlyList<string> RecentProjects { get; init; } = Array.Empty<string>();
}
