# Analyzers

## NuGet Packages Referenced

- AwesomeAssertions.Analyzers https://github.com/awesomeassertions/awesomeassertions.analyzers
- nunit.analyzers https://github.com/nunit/nunit.analyzers

All examples are located in `UnitTestingCookbook.Test` -> [`AnalyzersTest`](../UnitTestingCookbook.Test/AnalyzersTest.cs)

---

## What about repo-wide code quality analyzers - not test-specific ones?

The two packages above are about testing specifically. `Directory.Build.props` also references a set of
general-purpose analyzers, applied to **every project** in the solution (not just the test project) - there are
no NUnit tests for these, since a code-quality analyzer isn't something you unit test, but they're as much a part
of "how do I write good tests (and the code around them)" as anything else here:

- Meziantou.Analyzer https://github.com/meziantou/Meziantou.Analyzer (`MA*`)
- ErrorProne.NET https://github.com/SergeyTeplyakov/ErrorProne.NET (`EPC*`/`EPS*`)
- AsyncFixer https://github.com/semihokur/AsyncFixer (`AsyncFixerNN`)
- IDisposableAnalyzers https://github.com/DotNetAnalyzers/IDisposableAnalyzers (`IDISP*`)
- Microsoft.VisualStudio.Threading.Analyzers https://github.com/microsoft/vs-threading (`VSTHRD*`)
- Roslynator.Analyzers https://github.com/dotnet/roslynator (`RCS*`)
- StyleCop.Analyzers https://github.com/DotNetAnalyzers/StyleCopAnalyzers (`SA*`/`SX*`)

`.editorconfig` sets every one of these to `suggestion` severity by default (visible in the IDE and
`dotnet format analyzers`, never a build break) and promotes a short, deliberately curated list to `warning`
(`dotnet_diagnostic.severity = warning` overrides, grep `.editorconfig` for the exact set) - the idea being to
adopt individual rules over time rather than turning everything on at once and drowning in noise.

**NOTE:** installing the *analyzer* packages and setting a diagnostic ID's severity in `.editorconfig` are two
separate steps - `.editorconfig` can reference a diagnostic ID like `VSTHRD200` all day, but nothing produces that
diagnostic unless `Microsoft.VisualStudio.Threading.Analyzers` is actually referenced somewhere the compiler sees
it. This repo went a while with the severities configured but the packages never added - worth checking for if
you copy this `.editorconfig` into another repo without also copying `Directory.Build.props`.  

---

## How do I improve Assertions?

This example shows both `NUnit` and the transition to `AwesomeAssertions`.  
`nunit.analyzers` provides assistance to improving, along with `AwesomeAssertions.Analyzers`.  

`AwesomeAssertions.Analyzers` shows that...  
`list.All( b => b ).Should().BeTrue();`  
Can be improved to...  
`list.Should().OnlyContain( b => b );`  

:exclamation: For more examples... https://awesomeassertions.org/tips/#improved-assertions  

```csharp
public void A_AwesomeAssertions()
{
    // Arrange
    List<bool> list = new List<bool>() { true, true };

    // Act

    // Assert
    ClassicAssert.IsTrue(list.All(b => b)); // NUnit 4+: classic Assert.* moved to NUnit.Framework.Legacy.ClassicAssert
    Assert.That(list.All(b => b));

    list.All(b => b).Should().BeTrue();

    list.Should().OnlyContain(b => b);
}
```

---

## How do I avoid False Positives?

### Example 1 - Null-Conditional Operator `?.` short-circuit in Assert

AwesomeAssertions Analyzer will flag this code as possibly not executing.  

The statement will short-circuited and generate a false positive response, due to the Null-Conditional Operator in the Assert.  

```csharp
public void B_False_Positive()
{
    // Arrange
    string? name = null;

    // Act

    // Assert
    name?.Should().NotBeNull();
}
```

### Example 2 - `async void` with `throw`

The following code will compile, execute and report success.  
The problem lies in the `async void`.  
The Test Runner will not await the method.  
The Test Runner will report the Test as passed.  
When the `Exception` in thrown, the Test Runner will stop processing.  

If `nunit.analyzers` are loaded in the project, the Analyzer will have flagged this as an `Error` and would not allow compilation.  

```csharp
public async void FalsePositive()
{
    // Arrange

    // Act
    await Task.Delay(1000);
    throw new Exception("This Exception will not be reported and the test will succeed, but block other tests.");

    // Assert
    true.Should().BeTrue(); // our failed test will be reported as passed
}
```

---

Back to [README](../README.md)
