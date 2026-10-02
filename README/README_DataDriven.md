# Data Driven

## NuGet Packages Referenced

All examples are located in `UnitTestingCookbook.Tests` -> [`DataDrivenTests`](../UnitTestingCookbook.Tests/DataDrivenTests.cs)  

---

## How do I create a Data Driven test for value types?

Using `TestCaseAttribute`.  

NOTE: For value type data driven tests, it is best NOT to use a TestName.
Using TestName, especially with VS TestExplorer doesn't lend well to the visual appearance of the test names.  

<!-- snippet: DataDrivenTests_A_DataDrivenTest_TestCase -->
<a id='snippet-DataDrivenTests_A_DataDrivenTest_TestCase'></a>
```cs
[TestCase(1, 1, 2)]
[TestCase(0, 0, 0)]
[TestCase(-1, 1, 0)]
[Category("_passes")]
public void A_DataDrivenTest_TestCase(int a, int b, int expected)
{
    // Arrange

    // Act
    int result = a + b;

    // Assert
    result.Should().Be(expected);
}
```
<sup><a href='/UnitTestingCookbook.Tests/DataDrivenTests.cs#L30-L45' title='Snippet source file'>snippet source</a> | <a href='#snippet-DataDrivenTests_A_DataDrivenTest_TestCase' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## How do I create a Data Driven test for any object?

Using `TestCaseSourceAttribute`.  

<!-- snippet: DataDrivenTests_B_DataDrivenTest_TestCaseSource -->
<a id='snippet-DataDrivenTests_B_DataDrivenTest_TestCaseSource'></a>
```cs
[TestCaseSource(typeof(DataDrivenTestData), nameof(DataDrivenTestData.TestCaseSourceData))]
[Category("_passes")]
public void B_DataDrivenTest_TestCaseSource(Whale whale, bool expected)
{
    // Arrange

    // Act
    bool result = (whale?.Length ?? 0) > 0;

    // Assert
    result.Should().Be(expected);
}
```
<sup><a href='/UnitTestingCookbook.Tests/DataDrivenTests.cs#L50-L63' title='Snippet source file'>snippet source</a> | <a href='#snippet-DataDrivenTests_B_DataDrivenTest_TestCaseSource' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

```csharp
// This class can be named anything
// There can be multiple properties in this class
public static class DataDrivenTestData
{
    // This Property can be named anything
    // IEnumerable from System.Collections
    public static IEnumerable TestCaseSourceData
    {
        get
        {
            yield return new TestCaseData(new Whale() { Species = "Blue", Length = 60 }, true)
                .SetName("{m}[Blue Whale, Length of 60]");
            yield return new TestCaseData(new Whale() { Species = "Beluga", Length = 10 }, true)
                .SetName("{m}[Beluga Whale, Length of 10]");
            yield return new TestCaseData(new Whale() { Species = "Unicorn", Length = 0 }, false)
                .SetName("{m}[Unicorn Whale, Length of 0]");
            yield return new TestCaseData(null, false)
                .SetName("{m}[Null]");
        }
    }
}
```

---

Back to [README](../README.md)
