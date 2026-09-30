using Microsoft.Extensions.Configuration;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("parallel")]
[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class ParallelProcessingSafeTests
{
    [Test]
    [Category("_passes")]
    public void A_UsesOnlyLocalState_SafeToRunInParallel()
    {
        // Arrange
        List<int> numbers = new List<int> { 1, 2, 3 };

        // Act
        int sum = numbers.Sum();

        // Assert
        sum.Should().Be(6);
    }

    [Test]
    [Category("_passes")]
    public void B_UsesOnlyLocalState_SafeToRunInParallel()
    {
        // Arrange
        List<int> numbers = new List<int> { 10, 20, 30 };

        // Act
        int sum = numbers.Sum();

        // Assert
        sum.Should().Be(60);
    }

    [Test]
    [Category("_passes")]
    public void C_PerInstanceConfiguration_OutOfOrderDisposal_DoesNotInterfere()
    {
        // Arrange - compare against ParallelProcessingUnsafeTests.A: each "test" gets its own IConfiguration
        // instance instead of mutating the shared, process-wide environment (see Dependency Injection for the
        // IConfiguration setup pattern itself). There's no `using`/Dispose lifecycle at all here, because
        // there's no shared state to restore.
        IConfiguration configurationA = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MyKey"] = "test-a-value" })
            .Build();
        IConfiguration configurationB = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MyKey"] = "test-b-value" })
            .Build();

        // Act - order doesn't matter; neither instance can see or overwrite the other's value
        string? valueForA = configurationA["MyKey"];
        string? valueForB = configurationB["MyKey"];

        // Assert
        using (new AssertionScope())
        {
            valueForA.Should().Be("test-a-value");
            valueForB.Should().Be("test-b-value");
        }
    }
}
