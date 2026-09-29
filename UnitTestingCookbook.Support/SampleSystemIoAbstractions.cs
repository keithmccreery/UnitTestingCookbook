using System.IO.Abstractions;

namespace UnitTestingCookbook.Support;

/// <summary>
/// Sample System IO Abstractions
/// </summary>
/// <example>
/// <code>
/// IFileSystem filesystem = new FileSystem()
/// SystemIoAbstractions o = new SystemIoAbstractions( filesystem );
/// </code>
/// </example>
public class SampleSystemIoAbstractions
{
    private readonly IFileSystem fileSystem;

    public SampleSystemIoAbstractions(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public bool DoesFileExist(string path)
    {
        // was File.Exists( path )
        return fileSystem.File.Exists(path);
    }
}
