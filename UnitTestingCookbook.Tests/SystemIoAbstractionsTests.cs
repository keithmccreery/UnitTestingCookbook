using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

using UnitTestingCookbook.Support;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("system_io_abstractions")]
[TestFixture]
public class SystemIoAbstractionsTests
{
    //
    // Q: How do I test a class/method containing System.IO Objects?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: SystemIoAbstractionsTests_A_IFileSystem_Exist
    public void A_IFileSystem_Exist()
    {
        // Arrange
        const string existsFilePath = "this_file_exists.txt";
        const string doesNotExistFilePath = "this_file_does_not_exist.txt";

        IFileSystem fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
        {
            { existsFilePath, new MockFileData(String.Empty) },
        });

        SampleSystemIoAbstractions systemIoAbstractions = new SampleSystemIoAbstractions(fileSystem);

        // Act
        bool exists = systemIoAbstractions.DoesFileExist(existsFilePath);
        bool doesNotExist = systemIoAbstractions.DoesFileExist(doesNotExistFilePath);

        // Assert
        exists.Should().BeTrue();
        doesNotExist.Should().BeFalse();
    }
    // end-snippet
}
