using System.Runtime.CompilerServices;

namespace UnitTestingCookbook.Tests;

public static class VerifyModuleInitializer
{
    //
    // Runs once, when the test assembly loads - before any test (or NUnit itself) touches Verify.
    // Puts every *.verified.* / *.received.* file under UnitTestingCookbook.Tests/Snapshots/ instead of
    // next to each test's .cs file, so snapshots don't clutter the project root.
    //
    [ModuleInitializer]
    public static void Initialize()
    {
        Verifier.UseProjectRelativeDirectory("Snapshots");
    }
}
