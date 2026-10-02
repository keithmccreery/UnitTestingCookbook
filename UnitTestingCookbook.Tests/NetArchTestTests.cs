using System.Reflection;

using NetArchTest.Rules;

using UnitTestingCookbook.Support;

namespace UnitTestingCookbook.Tests;

//
// NetArchTest.Rules https://github.com/BenMorris/NetArchTest
//
// See AwesomeAssertions' Policy Assertions (README_AwesomeAssertions.md - V_PolicyAssertionForAsyncMethods,
// W_PolicyAssertionForAttributes) for the same idea - "assert a rule across a whole assembly, not one type at
// a time" - built on AwesomeAssertions' own reflection helpers instead of a dedicated library. NetArchTest is
// the purpose-built tool for this category: namespace/dependency rules in particular are awkward to hand-roll.
//
[Category("unit")]
[Category("netarchtest")]
[TestFixture]
public class NetArchTestTests
{
    private static readonly Assembly SupportAssembly = typeof(Miscellaneous).Assembly;

    //
    // Q: How do I stop "production" code from depending on a mocking/test library?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: NetArchTestTests_A_ShouldNotDependOnMoq
    public void A_ShouldNotDependOnMoq()
    {
        // Arrange

        // Act
        TestResult result = Types.InAssembly(SupportAssembly)
            .Should()
            .NotHaveDependencyOn("Moq")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }
    // end-snippet

    //
    // Q: How do I enforce a naming convention across a whole assembly?
    //
    // NOTE: .editorconfig's dotnet_naming_rule.interface_should_be_begins_with_i (see .editorconfig)
    // is the same rule, but only a suggestion visible in the IDE / `dotnet format`. This is the same
    // check as an actual test - it fails the build if it's ever violated, IDE or no IDE.
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: NetArchTestTests_B_InterfacesShouldStartWithI
    public void B_InterfacesShouldStartWithI()
    {
        // Arrange

        // Act
        TestResult result = Types.InAssembly(SupportAssembly)
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }
    // end-snippet

    //
    // Q: How do I stop library code from writing directly to the Console?
    //
    // Direct Console usage is a common source of "this class is hard to test" - see General Tips
    // (README_GeneralTips.md, A_CaptureConsole) for why: without redirecting Console, its output can't
    // be observed from a test at all. Scoped to the Services namespace specifically - the
    // Console.WriteLine calls elsewhere in this assembly (SampleWithLogging, Miscellaneous, ...) are
    // intentional demo instrumentation for the Logging chapter, not a violation this rule should catch.
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: NetArchTestTests_C_ServicesShouldNotDependOnConsole
    public void C_ServicesShouldNotDependOnConsole()
    {
        // Arrange

        // Act
        TestResult result = Types.InAssembly(SupportAssembly)
            .That()
            .ResideInNamespace("UnitTestingCookbook.Support.Services")
            .Should()
            .NotHaveDependencyOn("System.Console")
            .GetResult();

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccessful.Should().BeTrue();
            result.FailingTypeNames.Should().BeNullOrEmpty();
        }
    }
    // end-snippet
}
