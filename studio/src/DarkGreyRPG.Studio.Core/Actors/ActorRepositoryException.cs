namespace DarkGreyRPG.Studio.Core.Actors;

public class ActorRepositoryException : Exception
{
    public ActorRepositoryException(string message)
        : base(message)
    {
    }

    public ActorRepositoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ActorCollisionException : ActorRepositoryException
{
    public ActorCollisionException(string id)
        : base($"Actor ID '{id}' already exists in this project.")
    {
        ActorId = id;
    }

    public string ActorId { get; }
}

public sealed class ActorNotFoundException : ActorRepositoryException
{
    public ActorNotFoundException(string id)
        : base($"Actor '{id}' does not exist in this project.")
    {
        ActorId = id;
    }

    public string ActorId { get; }
}
