namespace DarkGreyRPG.Studio.Core.Items;

public class ItemRepositoryException : Exception
{
    public ItemRepositoryException(string message) : base(message) { }
    public ItemRepositoryException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class ItemCollisionException : ItemRepositoryException
{
    public ItemCollisionException(string id) : base($"Item ID or Group ID '{id}' already exists in this project.") => Id = id;
    public string Id { get; }
}

public sealed class ItemNotFoundException : ItemRepositoryException
{
    public ItemNotFoundException(string id) : base($"Item resource '{id}' does not exist in this project.") => Id = id;
    public string Id { get; }
}
