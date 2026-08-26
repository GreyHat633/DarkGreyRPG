namespace DarkGreyRPG.Studio.Core.Projects;

public sealed class ProjectException : Exception
{
    public ProjectException(string message)
        : base(message)
    {
    }

    public ProjectException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
