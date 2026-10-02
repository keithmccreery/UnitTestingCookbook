# AwesomeAssertions

## NuGet Packages Referenced

- AwesomeAssertions https://awesomeassertions.org/

**NOTE:** AwesomeAssertions is a community-maintained (Apache-2.0) fork of FluentAssertions, created after
FluentAssertions moved to a commercial license starting with v8. It kept the same fluent API (forked from
FluentAssertions v7), so almost everything below applies equally to either library - only the package name,
namespace, and a handful of renamed methods differ. See [Upgrading to v9](https://awesomeassertions.org/upgradingtov9)
if you're migrating an existing FluentAssertions codebase.

All examples are located in `UnitTestingCookbook.Tests` -> [`AwesomeAssertionsTests`](../UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs)

---

## What is AwesomeAssertions?

- A very extensive set of extension methods that allow you to more naturally specify the expected outcome of a TDD or BDD-style unit tests.
- A Fluent coding approach that provides a more natural language explanation of the Assertion.

---

## What does an Assertion look like in AwesomeAssertions?

AwesomeAssertions uses `.Should()` as the fluent builder.

<!-- snippet: AwesomeAssertionsTests_A_Simple -->
<a id='snippet-AwesomeAssertionsTests_A_Simple'></a>
```cs
public void A_Simple()
{
    // Arrange
    const string fullName = "John Doe";

    // Act

    // Assert
    fullName.Should().Be("John Doe");
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L23-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_A_Simple' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Why are AwesomeAssertions better than traditional Asserts?

### Answer 1

- Readability
- Better Error messages
- Subject identification

<!-- snippet: AwesomeAssertionsTests_B_SubjectIdentification -->
<a id='snippet-AwesomeAssertionsTests_B_SubjectIdentification'></a>
```cs
public void B_SubjectIdentification()
{
    // Arrange
    const string fullName = "John Doe";

    // Act

    // Assert
    fullName.Should().Be("Jane Doe");
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L44-L55' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_B_SubjectIdentification' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

When the Assertion is thrown, the following error will be produced.  

```text
Error Message...
Expected fullName to be "Jane Doe", but "John Doe" differs near "ohn" (index 1).
```

**Notice:** the subject is identified `fullName` and the index of the issue is provided.  

### Answer 2

With standard `Assert` (as of NUnit 4+, the classic assertions live in `NUnit.Framework.Legacy.ClassicAssert`)...  

<!-- snippet: AwesomeAssertionsTests_C_Assert_Vs_AwesomeAssertion_ErrorMessage -->
<a id='snippet-AwesomeAssertionsTests_C_Assert_Vs_AwesomeAssertion_ErrorMessage'></a>
```cs
public void C_Assert_Vs_AwesomeAssertion_ErrorMessage()
{
    // Arrange
    const string fullName = "John Doe";

    // Act

    // Assert
    ClassicAssert.AreEqual("Jane Doe", fullName);
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L71-L82' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_C_Assert_Vs_AwesomeAssertion_ErrorMessage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Will Produce the following error.

```text
String lengths are both 8. Strings differ at index 1.
Expected: "Jane Doe"
But was:  "John Doe"
------------^
```

**Notice:** no subject is identified and the index value has to be counted.  

---

## How do I add a custom message?

The last parameter of the Assertion (`.Be()`) is the `because` clause.  
Add a `because` (or `reason`).  

**NOTE:** The word `because` is already added in the error message, as is the trailing punctuation.  

<!-- snippet: AwesomeAssertionsTests_D_Custom_ErrorMessage -->
<a id='snippet-AwesomeAssertionsTests_D_Custom_ErrorMessage'></a>
```cs
public void D_Custom_ErrorMessage()
{
    // Arrange

    // Act

    // Assert
    100.Should().Be(99, "gas mileage should be 99");
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L92-L102' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_D_Custom_ErrorMessage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The error message is displayed as...

```text
Expected value to be 99 because gas mileage should be 99, but found 100 (difference of 1).
```

---

## How do I batch multiple Assertions?

Using the `AssertionScope` Object, all AwesomeAssertions in the block will be executed, regardless if a predecessor fails.  

<!-- snippet: AwesomeAssertionsTests_E_AssertionScope -->
<a id='snippet-AwesomeAssertionsTests_E_AssertionScope'></a>
```cs
public void E_AssertionScope()
{
    // Arrange

    // Act

    // Assert
    using (new AssertionScope())
    {
        "John Doe".Should().Be("Jane Doe");
        100.Should().Be(99);
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L113-L127' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_E_AssertionScope' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The output...

```text
Expected string to be "Jane Doe", but "John Doe" differs near "ohn" (index 1).
Expected value to be 99, but found 100 (difference of 1).
```

---

## How do I chain Assertions?

Using the `.And` Property.  
Once a failure occurs, the remaining chained Assertions are ignored.  

<!-- snippet: AwesomeAssertionsTests_F_ChainAssertions -->
<a id='snippet-AwesomeAssertionsTests_F_ChainAssertions'></a>
```cs
public void F_ChainAssertions()
{
    // Arrange

    // Act

    // Assert
    "John Doe".Should().NotBeNull() // .And allows the chaining
        .And.HaveLength(8)
        .And.StartWith("John")
        .And.EndWith("Doe");
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L134-L147' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_F_ChainAssertions' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## What Types can be asserted?

https://awesomeassertions.org/introduction

---

## How do I Assert on a list with a single Object?

Using the `.ContainSingle()` Assertion. When `true`, `.ContainSingle()` will return *the* single Object.  
To chaining additional Assertions, use the `.Which` property to obtain the object and Assert on the object or its Properties.  

<!-- snippet: AwesomeAssertionsTests_G_ContainSingle -->
<a id='snippet-AwesomeAssertionsTests_G_ContainSingle'></a>
```cs
public void G_ContainSingle()
{
    // Arrange
    List<Animal> animals = new List<Animal>()
    {
        new Animal() { Species = "Whale" },
    };

    // Act

    // Assert
    animals.Should().ContainSingle()
        .Which.Species.Should().Be("Whale"); // .Which allows the chaining from the conversion
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L154-L169' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_G_ContainSingle' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I downcast?

Using `.As<T>()` to cast.  

<!-- snippet: AwesomeAssertionsTests_H_Downcast -->
<a id='snippet-AwesomeAssertionsTests_H_Downcast'></a>
```cs
public void H_Downcast()
{
    // Arrange
    List<Animal> animals = new List<Animal>()
    {
        new Whale() { Species = "Whale", Length = 100 },
    };

    // Act

    // Assert
    animals[0].As<Whale>().Length.Should().Be(100);
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L176-L190' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_H_Downcast' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I assert on all items of a collection, individually?

Each answer must account for all items in the list.  

### Answer 1

Using `.AllSatisfy()`, to Assert all the items of the collection with the **same** criteria.  

<!-- snippet: AwesomeAssertionsTests_I_AllSatisfy -->
<a id='snippet-AwesomeAssertionsTests_I_AllSatisfy'></a>
```cs
public void I_AllSatisfy()
{
    // Arrange
    List<Animal> animals = new List<Animal>()
    {
        new Whale() { Species = "Humpback", Length = 100 },
        new Whale() { Species = "Blue", Length = 60 },
    };

    // Act

    // Assert
    animals.Should().HaveCountGreaterThanOrEqualTo(2)
        .And.AllSatisfy(x =>
    {
        x.Species.Should().NotBeNull();
        ((Whale) x).Length.Should().BeGreaterThan(50);
    });
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L197-L217' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_I_AllSatisfy' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Answer 2

Using `.SatisfyRespectively()`, to Assert the collection individually, in order.  

**NOTE:** `.SatisfyRespectively()` delegates are `Action`s to perform.  

<!-- snippet: AwesomeAssertionsTests_I_SatisfyRespectively -->
<a id='snippet-AwesomeAssertionsTests_I_SatisfyRespectively'></a>
```cs
public void I_SatisfyRespectively()
{
    // Arrange
    List<Animal> animals = new List<Animal>()
    {
        new Whale() { Species = "Humpback", Length = 100 },
        new Whale() { Species = "Blue", Length = 60 },
    };

    // Act

    // Assert
    animals.Should().SatisfyRespectively(
        first =>
        {
            first.Species.Should().Be("Humpback");
            first.Should().BeOfType<Whale>() // .BeOfType<>() will cast to Whale
                .Which.Length.Should().Be(100); // .Which returns the item (Whale)
        },
        second =>
        {
            second.Species.Should().Be("Blue");
            second.Should().BeOfType<Whale>() // .BeOfType<>() will cast to Whale
                .Which.Length.Should().Be(60); // .Which returns the item (Whale)
        }
    );
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L224-L252' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_I_SatisfyRespectively' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Answer 3

Using `.Satisfy()`, to Assert individually, in any order, having all items been matched.  

**NOTE:** `.Satisfy()` delegates are `Func`s to evaluate to `true` or `false`.  

<!-- snippet: AwesomeAssertionsTests_I_Satisfy -->
<a id='snippet-AwesomeAssertionsTests_I_Satisfy'></a>
```cs
public void I_Satisfy()
{
    // Arrange
    List<Animal> animals = new List<Animal>()
    {
        new Whale() { Species = "Humpback", Length = 100 },
        new Whale() { Species = "Blue", Length = 60 },
    };

    // Act

    // Assert
    animals.Should().Satisfy(
        x => x.GetType() == typeof(Whale)
            && x.Species == "Blue"
            && ((Whale) x).Length == 60, // must box
        x => x.GetType() == typeof(Whale)
            && x.Species == "Humpback"
            && ((Whale) x).Length == 100 // must box
    );
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L259-L281' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_I_Satisfy' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I Assert that a Dictionary contains a specific value?

Using `.ContainValue()` Assertion.  

**NOTE:** `.ContainValue()` uses `.Equal( object )` under the hood.  

<!-- snippet: AwesomeAssertionsTests_K_ContainValue -->
<a id='snippet-AwesomeAssertionsTests_K_ContainValue'></a>
```cs
public void K_ContainValue()
{
    // Arrange
    Whale whale = new Whale() { Species = "Blue", Length = 60 };

    Dictionary<string, Whale> critters = new Dictionary<string, Whale>()
    {
        { "Blue", whale },
    };

    // Act

    // Assert
    critters.Should().ContainValue(whale); // uses .Equal( object )
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L288-L304' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_K_ContainValue' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I assert an Exception?

Using `.Throw<T>()` or `.ThrowExactly<T>()` Assertion.  
This example show additional Assertions and multiple ways to Assert the `Message` Property.  

<!-- snippet: AwesomeAssertionsTests_L_Exception -->
<a id='snippet-AwesomeAssertionsTests_L_Exception'></a>
```cs
public void L_Exception()
{
    // Arrange
    Miscellaneous miscellaneous = new Miscellaneous();

    // Act
    Action action = () => miscellaneous.ThrowsAnExceptionWithInnerException();

    // Assert
    action.Should().Throw<Exception>()
        .Where(e => e.Message.EndsWith("exception"));

    action.Should().ThrowExactly<InvalidOperationException>()
        .WithMessage("original exception")
        .WithInnerExceptionExactly<NullReferenceException>()
        .WithMessage("inner exception");
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L311-L329' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_L_Exception' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I assert an Exception on async code?

By using `Func<Task> action = () =>` and `await`ing the `action`.  

<!-- snippet: AwesomeAssertionsTests_M_ExceptionAsync -->
<a id='snippet-AwesomeAssertionsTests_M_ExceptionAsync'></a>
```cs
public async Task M_ExceptionAsync()
{
    // Arrange
    Miscellaneous miscellaneous = new Miscellaneous();

    // Act
    Func<Task> action = () => miscellaneous.ThrowsAnExceptionWithInnerExceptionAsync();

    // Assert
    await action.Should().ThrowExactlyAsync<InvalidOperationException>();
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L336-L348' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_M_ExceptionAsync' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I assert an Inner Exception on async code?

`.WithInnerException<T>()` and `.WithInnerExceptionExactly<T>()` do not work with a Generic with `async` code.
The Type must be supplied as a parameter.  

<!-- snippet: AwesomeAssertionsTests_N_ExceptionAndInnerExceptionAsync -->
<a id='snippet-AwesomeAssertionsTests_N_ExceptionAndInnerExceptionAsync'></a>
```cs
public async Task N_ExceptionAndInnerExceptionAsync()
{
    // Arrange
    Miscellaneous miscellaneous = new Miscellaneous();

    // Act
    Func<Task> action = () => miscellaneous.ThrowsAnExceptionWithInnerExceptionAsync();

    // Assert
    await action.Should().ThrowExactlyAsync<InvalidOperationException>()
        .WithMessage("original exception")
        .WithInnerExceptionExactly(typeof(NullReferenceException)) // can't use generic WithInnerException<T>() or WithInnerExceptionExactly<T>() with async
        .WithMessage("inner exception");
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L355-L370' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_N_ExceptionAndInnerExceptionAsync' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I assert on code that returns an IEnumerable using yield?

By using `Func<IEnumerable<string>> action = () =>` and the `Enumerating()` Method on the `action`. 

<!-- snippet: AwesomeAssertionsTests_O_IEnumerableYield -->
<a id='snippet-AwesomeAssertionsTests_O_IEnumerableYield'></a>
```cs
public void O_IEnumerableYield()
{
    // Arrange
    const string? input = null;

    // Act
    Func<IEnumerable<string>> action = () => input!.SplitAndKeep(new char[] { ' ' });

    // Assert
    action.Enumerating().Should().ThrowExactly<ArgumentNullException>();
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L377-L389' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_O_IEnumerableYield' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I compare Objects?

By using the Object Graph Assertions.

<!-- snippet: AwesomeAssertionsTests_P_ObjectComparison -->
<a id='snippet-AwesomeAssertionsTests_P_ObjectComparison'></a>
```cs
public void P_ObjectComparison()
{
    // Arrange
    Whale atlanticWhale = new Whale() { Species = "Blue", Length = 60 };
    Whale pacificWhale = new Whale() { Species = "Blue", Length = 60 };

    // Assert

    // Act
    atlanticWhale.Should().BeEquivalentTo(pacificWhale);
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L396-L408' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_P_ObjectComparison' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I compare an Object to an Anonymous Object with missing Members?

Using `.BeEquivalentTo()` with the option to exclude missing members `options => options.ExcludingMissingMembers()`.  

**NOTE:** `options` are chained.  

<!-- snippet: AwesomeAssertionsTests_Q_AnonymousObjectComparison -->
<a id='snippet-AwesomeAssertionsTests_Q_AnonymousObjectComparison'></a>
```cs
public void Q_AnonymousObjectComparison()
{
    // Arrange
    Whale whale = new Whale() { Species = "Blue", Length = 60 };

    // Assert

    // Act
    whale.Should().BeEquivalentTo(new
    {
        Species = "Blue",
    }, options => options.ExcludingMissingMembers());
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L415-L429' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_Q_AnonymousObjectComparison' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I override the comparison on a Member when comparing Objects?

Using `options.Using<int>()` and including the comparison - either between the 2 objects or with just the subject.  

<!-- snippet: AwesomeAssertionsTests_R_ObjectComparisonWithUsing -->
<a id='snippet-AwesomeAssertionsTests_R_ObjectComparisonWithUsing'></a>
```cs
public void R_ObjectComparisonWithUsing()
{
    // Arrange
    Whale whale = new Whale() { Species = "Blue", Length = 60 };

    // Assert

    // Act
    whale.Should()
        .BeEquivalentTo(new // can be and object or anonymous
        {
            Species = "Blue",
        }, options => options
            .Using<int>(ctx => ctx.Subject.Should().BeGreaterThanOrEqualTo(50)) // could compare against ctx.Expectation
            .When(p => p.Path.EndsWith("Length")) // .When( lambda ) or .WhenTypeIs<T>() is required
        );
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L436-L454' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_R_ObjectComparisonWithUsing' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I Assert an Event?

1. Call `.Monitor()` on the Object.  
1. Raise the Even on the Object.  
1. Call `.Raise()` Assertion to Assert.  

<!-- snippet: AwesomeAssertionsTests_S_Events -->
<a id='snippet-AwesomeAssertionsTests_S_Events'></a>
```cs
public void S_Events()
{
    // Arrange
    Miscellaneous miscellaneous = new Miscellaneous();

    using var miscellaneousMonitor = miscellaneous.Monitor();

    // Act
    miscellaneous.RaiseDoSomethingHappened();

    // Assert
    miscellaneousMonitor.Should().Raise("SomethingHappenedEvent");
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L461-L475' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_S_Events' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I Assert the execution time of a Method?

Using `.ExecutionTimeOf()` Assertion.

<!-- snippet: AwesomeAssertionsTests_T_ExecutionTimeO -->
<a id='snippet-AwesomeAssertionsTests_T_ExecutionTimeO'></a>
```cs
public void T_ExecutionTimeO()
{
    // Arrange
    Miscellaneous miscellaneous = new Miscellaneous();

    // Act

    // Assert
    miscellaneous
        .ExecutionTimeOf(s => s.SlowRunningMethod())
        .Should().BeLessThanOrEqualTo(500.Milliseconds());
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L482-L495' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_T_ExecutionTimeO' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**Hint:** Never measure the execution time of code that you don't own (e.g. a service endpoint).
You have no control over the execution or the result.

---

## How do I assert the execution time of an async Method?

Setup with `Func<Task<T>> work = () =>` and `await` the `action` and Assert with `.CompleteWithinAsync()`.   

<!-- snippet: AwesomeAssertionsTests_U_CompleteWithinAsyncWithResult -->
<a id='snippet-AwesomeAssertionsTests_U_CompleteWithinAsyncWithResult'></a>
```cs
public async Task U_CompleteWithinAsyncWithResult()
{
    // Arrange
    Func<Task<int>> work = () => Task.FromResult<int>(-1);

    // Act

    // Assert
    // 500ms, not e.g. 500 microseconds: a shared CI runner under load can blow through a sub-millisecond
    // bound on pure scheduling jitter alone, even for an already-completed Task like this one - this
    // flaked intermittently in CI before being loosened.
    await work.Should().CompleteWithinAsync(500.Milliseconds()).WithResult(-1);
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L502-L516' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_U_CompleteWithinAsyncWithResult' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Policy Assertions (on Types, Methods and Properties)

### How do I ensure all async methods are suffixed 'Async'?

<!-- snippet: AwesomeAssertionsTests_V_PolicyAssertionForAsyncMethods -->
<a id='snippet-AwesomeAssertionsTests_V_PolicyAssertionForAsyncMethods'></a>
```cs
public void V_PolicyAssertionForAsyncMethods()
{
    // Arrange

    // Act

    // Assert
    using (new AssertionScope())
    {
        // Miscellaneous Class

        // All async methods must end with "Async"
        typeof(Miscellaneous).Methods()
            .ThatAreAsync()
            .Should()
            .SubjectMethods
            .All(m => m.Name.EndsWith("Async")).Should().BeTrue();

        // Any methods that return void, must not be async
        typeof(Miscellaneous).Methods()
            .ThatReturnVoid
            .Should().NotBeAsync();

        // All async methods must have the last parameter as a CancellationToken
        typeof(Miscellaneous).Methods()
            .ThatAreAsync()
            .Should()
            .SubjectMethods
            .All(m => m.GetParameters()[^1].ParameterType == typeof(CancellationToken)).Should().BeTrue();
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L523-L555' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_V_PolicyAssertionForAsyncMethods' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### How do I ensure all Test Methods have a Category?

<!-- snippet: AwesomeAssertionsTests_W_PolicyAssertionForAttributes -->
<a id='snippet-AwesomeAssertionsTests_W_PolicyAssertionForAttributes'></a>
```cs
public void W_PolicyAssertionForAttributes()
{
    // Arrange

    // Act

    // Assert
    this.GetType().Assembly.Types() // get all classes from the test assembly
        .Methods() // get all methods on all classes
        .ThatAreDecoratedWith<TestAttribute>() // get only test methods
        .Should().BeDecoratedWith<CategoryAttribute>(); // verify they have a Category assigned
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L562-L575' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_W_PolicyAssertionForAttributes' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How can I assert XML?

Using `.HaveRoot()`, `.HaveElement()`, `.HaveAttribute()`, etc... on `XDocument` or `XElement` Objects.  

<!-- snippet: AwesomeAssertionsTests_X_XML -->
<a id='snippet-AwesomeAssertionsTests_X_XML'></a>
```cs
public void X_XML()
{
    // Arrange
    XElement children = new XElement("Children",
        new XElement("Child", "John"),
        new XElement("Child", "Jane")
    );
    children.SetAttributeValue("count", 2);

    XDocument document = new XDocument(
        new XComment("This is a comment"),
        new XElement("Root",
            new XElement("Parents",
                new XElement("Parent", "Zoe"),
                new XElement("Parent", "Zeus")
            ),
            children
        )
    );

    XElement? parents = document.Root?.Element("Parents");

    // Act

    // Assert
    using (new AssertionScope())
    {
        document.Should().HaveRoot("Root");
        document.Should().HaveElement("Children", Exactly.Once());

        children.Should().HaveAttribute("count", "2");

        parents.Should().HaveElement("Parent", Exactly.Twice());
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L582-L618' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_X_XML' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I Assert HttpResponseMessage StatusCode?

FluentAssertions/AwesomeAssertions dropped the generic `.HaveStatusCode()` assertion on `HttpResponseMessage`
(the [AwesomeAssertions Add-Ons](./README_AwesomeAssertionsAddOns.md) `AwesomeAssertions.Web` package only exposes
status-specific methods like `.Be200Ok()`). For a simple StatusCode check, just assert the property directly.  

<!-- snippet: AwesomeAssertionsTests_Y_HttpResponseMessage -->
<a id='snippet-AwesomeAssertionsTests_Y_HttpResponseMessage'></a>
```cs
public void Y_HttpResponseMessage()
{
    // Arrange
    HttpResponseMessage httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);

    // Act

    // Assert
    httpResponseMessage.StatusCode.Should().Be(HttpStatusCode.OK);
}
```
<sup><a href='/UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs#L630-L641' title='Snippet source file'>snippet source</a> | <a href='#snippet-AwesomeAssertionsTests_Y_HttpResponseMessage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

Back to [README](../README.md)
