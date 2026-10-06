using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.ViewModels;

public static class ResourceIdentityPresentation
{
    public static string Format(string label, string id)
        => StoryUid.IsValid(id) ? $"Story UID：{id}" : label switch
        {
            "NPC_ID" => "[角色]", "Group_ID" => "[角色组]", "Item_ID" => "[物品]",
            "ItemGroup_ID" => "[物品组]", "Session_ID" => "[会话]", "Task_ID" => "[任务]", _ => string.Empty,
        };
}
