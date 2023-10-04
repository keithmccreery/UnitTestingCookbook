using System.Collections;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Test;

//
// NUnit Documentation https://docs.nunit.org/articles/nunit/writing-tests/attributes/testcasesource.html
//
// MSTest Documentation https://learn.microsoft.com/en-us/visualstudio/test/how-to-create-a-data-driven-unit-test?view=vs-2022
//
[Category( "unit" )]
[Category( "datadriven" )]
[TestFixture]
public class DataDrivenTest
{
    //
    // Q: How do I create a Data Driven test for value types?
    //
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

    //
    // Q: How do I create a Data Driven test for any object?
    //
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
}

// This class can be name anything
// There can be multiple properties in this class
public static class DataDrivenTestData
{
    // This Property can be name anything
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
