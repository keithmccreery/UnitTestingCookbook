using System.Collections;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Test;

//
// NUnit Documentation
//   TestCaseSource https://docs.nunit.org/articles/nunit/writing-tests/attributes/testcasesource.html
//   TestCaseData https://docs.nunit.org/articles/nunit/writing-tests/TestCaseData.html
//   Template-Based Testing Naming https://docs.nunit.org/articles/nunit/running-tests/Template-Based-Test-Naming.html
//
// MSTest Documentation
//   https://learn.microsoft.com/en-us/visualstudio/test/how-to-create-a-data-driven-unit-test?view=vs-2022
//
[Category( "unit" )]
[Category( "datadriven" )]
[TestFixture]
public class DataDrivenTest
{
    //
    // Q: How do I create a Data Driven test for value types?
    //
    // NOTE: For value type data driven tests, it is best NOT to use a TestName.
    //       Using TestName, especially with VS TestExplorer doesn't lend well to
    //       the visual appearance of the test names.
    //
    [TestCase( 1, 1, 2 )]
    [TestCase( 0, 0, 0 )]
    [TestCase( -1, 1, 0 )]
    [Category( "_passes" )]
    public void A_DataDrivenTest_TestCase( int a, int b, int expected )
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
            yield return new TestCaseData( new Whale() { Species = "Blue", Length = 60 }, true )
                .SetName( "{m}[ Blue Whale, Length of 60 ]" );
            yield return new TestCaseData( new Whale() { Species = "Beluga", Length = 10 }, true )
                .SetName( "{m}[ Beluga Whale, Length of 10 ]" );
            yield return new TestCaseData( new Whale() { Species = "Unicorn", Length = 0 }, false )
                .SetName( "{m}[ Unicorn Whale, Length of 0 ]" );
            yield return new TestCaseData( null, false )
                .SetName( "{m}[ Null ]" );
        }
    }
}
