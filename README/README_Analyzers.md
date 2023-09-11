# Analyzers

## NuGet Packages Referenced

- FluentAssertions.Analyzers https://github.com/fluentassertions/fluentassertions.analyzers
- nunit.analyzers https://github.com/nunit/nunit.analyzers

All examples are located in `UnitTestingCookbook.Test` -> [`AnalyzersTest`](../UnitTestingCookbook.Test/AnalyzersTest.cs)

---

## How do I improve Assertions?

This example shows both `NUnit` and the transition to `FluentAssertions`.  
`nunit.analyzers` provides assistance to improving, along with `FluentAssertions.Analyzers`.  

`FluentAssertions.Analyzers` shows that...  
`list.All( b => b ).Should().BeTrue();`  
Can be improved to...  
`list.Should().OnlyContain( b => b );`  

:exclamation: For more examples... https://fluentassertions.com/tips/#improved-assertions  

```csharp
public void A_FluentAssertions()
{
    // Arrange
    List<bool> list = new List<bool>() { true, true };

    // Act

    // Assert
    Assert.IsTrue( list.All( b => b ) );
    Assert.That( list.All( b => b ) );

    list.All( b => b ).Should().BeTrue();

    list.Should().OnlyContain( b => b );
}
```

---

## How do I avoid False Positives?

### Example 1 - Null-Conditional Operator `?.` short-circuit in Assert

FluentAssertion Analyzer will flag this code as possibly not executing.  

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
    await Task.Delay( 1000 );
    throw new Exception( "This Exception will not be reported and the test will succeed, but block other tests." );

    // Assert
    true.Should().BeTrue(); // our failed test will be reported as passed
}
```

---

Back to [README](../README.md)
