namespace DarkGreyRPG.Studio.Core.Identity;

public enum DgrResourceKind { Story, Actor, Item, ItemGroup, Session, Task }
public readonly record struct DgrResourceKey(DgrResourceKind Kind, string Id);
