namespace DarkGreyRPG.Studio.Core.IO;

public interface IAtomicFileWriter
{
    void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null);
}
