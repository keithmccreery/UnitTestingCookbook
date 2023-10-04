# Data Driven

## NuGet Packages Referenced

All examples are located in `UnitTestingCookbook.Test` -> [`DataDrivenTest`](../UnitTestingCookbook.Test/DataDrivenTest.cs)  

---

## How do I create a Data Driven test for value types?

Using `TestCaseAttribute`.  

```csharp
[TestCase( 1, 1, 2, TestName = "1 + 1")]
[TestCase( 0, 0, 0, TestName = "0 + 0" )]
[TestCase( -1, 1, 0, TestName = "-1 + 1" )]
[Category( "_passes" )]
public void A_DataDrivenTest_TestCase( int a, int b, int expected)
{
    // Arrange

    // Act
    int result = a + b;

    // Assert
    result.Should().Be( expected );
}
```

## How do I create a Data Driven test for any object?

Using `TestCaseSourceAttribute`.  

```csharp
[TestCaseSource( typeof( DataDrivenTestData ), nameof( DataDrivenTestData.TestCaseSourceData ) )]
[Category( "_passes" )]
public void B_DataDrivenTest_TestCaseSource( Whale whale, bool expected )
{
    // Arrange

    // Act
    bool result = ( whale?.Length ?? 0 ) > 0;

    // Assert
    result.Should().Be( expected );
}
```

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
            yield return new TestCaseData( new Whale() { Species = "Blue", Length = 60 }, true ) { TestName = "Blue Whale, Length of 60" };
            yield return new TestCaseData( new Whale() { Species = "Beluga", Length = 10 }, true ) { TestName = "Beluga Whale, Length of 10" };
            yield return new TestCaseData( new Whale() { Species = "Unicorn", Length = 0 }, false ) { TestName = "Unicorn Whale, Length of 0" };
            yield return new TestCaseData( null, false ) { TestName = "Null" };
        }
    }
}
```

---

Back to [README](../README.md)
