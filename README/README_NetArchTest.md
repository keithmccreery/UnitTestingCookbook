# NetArchTest

## NuGet Packages Referenced

- NetArchTest.Rules https://github.com/BenMorris/NetArchTest

All examples are located in `UnitTestingCookbook.Test` -> [`NetArchTestTest`](../UnitTestingCookbook.Test/NetArchTestTest.cs)

---

## What is NetArchTest?

A fluent API for asserting **architecture** rules - not "does this method return the right value," but "does
this whole assembly (or namespace, or set of types) follow a rule" - as an ordinary test that fails the build if
the rule is ever violated.

See [AwesomeAssertions](./README_AwesomeAssertions.md)'s Policy Assertions section
(`V_PolicyAssertionForAsyncMethods`, `W_PolicyAssertionForAttributes`) for the same idea built on
AwesomeAssertions' own reflection helpers instead of a dedicated library - that works fine for method-level rules.
NetArchTest is the purpose-built tool for this category; namespace/dependency rules in particular (see below) are
awkward to hand-roll with plain reflection.

---

## How do I stop "production" code from depending on a mocking/test library?

```csharp
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
```

---

## How do I enforce a naming convention across a whole assembly?

```csharp
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
```

**NOTE:** `.editorconfig`'s `dotnet_naming_rule.interface_should_be_begins_with_i` is the same rule, but only a
suggestion visible in the IDE / `dotnet format`. This is the same check as an actual test - it fails the build if
it's ever violated, IDE or no IDE.  

---

## How do I stop library code from writing directly to the Console?

Direct `Console` usage is a common source of "this class is hard to test" - see
[General Tips](./README_GeneralTips.md)'s `A_CaptureConsole` for why: without redirecting `Console`, its output
can't be observed from a test at all.

```csharp
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
```

**NOTE:** This is scoped to the `Services` namespace specifically, not the whole assembly - the
`Console.WriteLine` calls elsewhere in `UnitTestingCookbook.Support` (`SampleWithLogging`, `Miscellaneous`, ...)
are intentional demo instrumentation for the [Logging](./README_Logging.md) chapter, not a violation this rule
should catch. Scoping the rule to exactly the part of the codebase the rule is actually about - rather than the
whole assembly - is itself part of writing a useful architecture test: too broad, and it either false-positives
on legitimate exceptions or gets disabled the first time it's inconvenient.  

---

Back to [README](../README.md)
