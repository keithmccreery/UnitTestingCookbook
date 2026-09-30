using System.Net;
using System.Xml.Linq;

using AwesomeAssertions.Extensions;

using NUnit.Framework.Legacy;

using UnitTestingCookbook.Support;
using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("awesomeassertions")]
[TestFixture]
public class AwesomeAssertionsTests
{
    //
    // Q: What does an assertion look like in AwesomeAssertions?
    //
    [Test]
    [Category("_passes")]
    public void A_Simple()
    {
        // Arrange
        const string fullName = "John Doe";

        // Act

        // Assert
        fullName.Should().Be("John Doe");
    }

    //
    // Q: Why are AwesomeAssertions better than traditional Asserts (part 1)?
    //
    // Error Message...
    // Expected fullName to be "Jane Doe", but "John Doe" differs near "ohn" (index 1).
    //
    [Test]
    [Category("_fails")]
    public void B_SubjectIdentification()
    {
        // Arrange
        const string fullName = "John Doe";

        // Act

        // Assert
        fullName.Should().Be("Jane Doe");
    }

    //
    // Q: Why are AwesomeAssertions better than traditional Asserts (part 2)?
    //
    // Assert Error Message...
    // String lengths are both 8. Strings differ at index 1.
    // Expected: "Jane Doe"
    // But was:  "John Doe"
    // ------------^
    //
    // AwesomeAssertion Error Message...
    // Expected fullName to be "Jane Doe", but "John Doe" differs near "ohn" (index 1).
    //
    [Test]
    [Category("_fails")]
    public void C_Assert_Vs_AwesomeAssertion_ErrorMessage()
    {
        // Arrange
        const string fullName = "John Doe";

        // Act

        // Assert
        ClassicAssert.AreEqual("Jane Doe", fullName);
    }

    //
    // Q: How do I add a custom error message?
    //
    // Error Message...
    // Expected value to be 99 because gas mileage should be 99, but found 100 (difference of 1).
    //
    [Test]
    [Category("_fails")]
    public void D_Custom_ErrorMessage()
    {
        // Arrange

        // Act

        // Assert
        100.Should().Be(99, "gas mileage should be 99");
    }

    //
    // Q: How do I batch multiple assertions?
    //
    // Error Message...
    // Expected string to be "Jane Doe", but "John Doe" differs near "ohn" (index 1).
    // Expected value to be 99, but found 100 (difference of 1).
    //
    [Test]
    [Category("_fails")]
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

    //
    // Q: How do I chain assertions?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I Assert on a list with a single Object?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I downcast?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I assert on all items of a collection, individually (part 1)?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I assert on all items of a collection, individually (part 2)?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I assert on all items of a collection, individually (part 3)?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I Assert that a Dictionary contains a specific value?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I assert an Exception?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I assert an Exception on async code?
    //
    [Test]
    [Category("_passes")]
    public async Task M_ExceptionAsync()
    {
        // Arrange
        Miscellaneous miscellaneous = new Miscellaneous();

        // Act
        Func<Task> action = () => miscellaneous.ThrowsAnExceptionWithInnerExceptionAsync();

        // Assert
        await action.Should().ThrowExactlyAsync<InvalidOperationException>();
    }

    //
    // Q: How do I assert an Inner Exception on async code?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I assert on code that returns an IEnumerable using yield?
    //
    [Test]
    [Category("_passes")]
    public void O_IEnumerableYield()
    {
        // Arrange
        const string? input = null;

        // Act
        Func<IEnumerable<string>> action = () => input!.SplitAndKeep(new char[] { ' ' });

        // Assert
        action.Enumerating().Should().ThrowExactly<ArgumentNullException>();
    }

    //
    // Q: How do I compare Objects?
    //
    [Test]
    [Category("_passes")]
    public void P_ObjectComparison()
    {
        // Arrange
        Whale atlanticWhale = new Whale() { Species = "Blue", Length = 60 };
        Whale pacificWhale = new Whale() { Species = "Blue", Length = 60 };

        // Assert

        // Act
        atlanticWhale.Should().BeEquivalentTo(pacificWhale);
    }

    //
    // Q: How do I compare an Object to an Anonymous Object with missing Members?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I override the comparison on a Member when comparing Objects?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I Assert an Event?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I Assert the execution time of a Method?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I Assert the execution time of an async Method?
    //
    [Test]
    [Category("_passes")]
    public async Task U_CompleteWithinAsyncWithResult()
    {
        // Arrange
        Func<Task<int>> work = () => Task.FromResult<int>(-1);

        // Act

        // Assert
        await work.Should().CompleteWithinAsync(500.Microseconds()).WithResult(-1);
    }

    //
    // Q: How do I ensure all async methods are suffixed 'Async'
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I ensure all Test Methods have a Category?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How can I assert XML?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I Assert HttpResponseMessage StatusCode?
    //
    // NOTE: FluentAssertions/AwesomeAssertions dropped the generic HaveStatusCode() assertion on
    // HttpResponseMessage; the AwesomeAssertions.Web addon (see AwesomeAssertionsAddOnsTest.cs) only
    // exposes status-specific methods like Be200Ok(). Asserting the StatusCode property directly avoids
    // the addon dependency for this simple case.
    //
    [Test]
    [Category("_passes")]
    public void Y_HttpResponseMessage()
    {
        // Arrange
        HttpResponseMessage httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        // Act

        // Assert
        httpResponseMessage.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
