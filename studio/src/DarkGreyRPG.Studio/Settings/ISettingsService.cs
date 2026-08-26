namespace DarkGreyRPG.Studio.Settings;

public interface ISettingsService
{
    string SettingsPath { get; }

    StudioSettings Load();

    void Save(StudioSettings settings);
}
