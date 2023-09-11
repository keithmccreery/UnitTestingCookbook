# System.IO.Abstractions

## NuGet Packages Referenced

- System.IO.Abstractions https://github.com/TestableIO/System.IO.Abstractions

All examples are located in `UnitTestingCookbook.Test` -> [`SystemIoAbstractionsTest`](../UnitTestingCookbook.Test/SystemIoAbstractionsTest.cs)  

---

## How do I test a class/method containing System.IO Objects?

### Answer 1

You Can't. There are no interfaces for the `System.IO` namespace Objects.  

### Answer 2

Use `System.IO.Abstractions`.  

**Caveat Emptor**, using `System.IO.Abstractions` will REQUIRE code changes, but will ALLOW for testing.  

Consider this example class, which uses `System.IO.Abstractions`.
It will implement Dependency Injection allowing for easier testing.  

```csharp
using System.IO.Abstractions;

namespace UnitTestingCookbook.Support;

public class SampleSystemIoAbstractions
{
    private readonly IFileSystem fileSystem;

    public SampleSystemIoAbstractions( IFileSystem fileSystem )
    {
        this.fileSystem = fileSystem;
    }

    public bool DoesFileExist( string path )
    {
        // was File.Exists( path )
        return fileSystem.File.Exists( path );
    }
}
```

Sample Unit test...  
We can create a Mock File System, with Mock Files and Mock Data to test against.  

```csharp
public void A_IFileSystem_Exist()
{
    // Arrange
    const string existsFilePath = "this_file_exists.txt";
    const string doesNotExistFilePath = "this_file_does_not_exist.txt";

    IFileSystem fileSystem = new MockFileSystem( new Dictionary<string, MockFileData>()
    {
        { existsFilePath, new MockFileData( String.Empty ) },
    });

    SampleSystemIoAbstractions systemIoAbstractions = new SampleSystemIoAbstractions( fileSystem );

    // Act
    bool exists = systemIoAbstractions.DoesFileExist( existsFilePath );
    bool doesNotExist = systemIoAbstractions.DoesFileExist( doesNotExistFilePath );

    // Assert
    exists.Should().BeTrue();
    doesNotExist.Should().BeFalse();
}
```

In Production, use the default `FileSystem`, which calls `System.IO`.  

```csharp
services.AddSingleton<IFileSystem,FileSystem>();
```

---

Back to [README](../README.md)
