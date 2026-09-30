using UnitTestingCookbook.TestHelpers;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("parallel")]
[TestFixture]
public class ParallelProcessingUnsafeTests
{
    [Test]
    [Category("_passes")]
    public void A_OverlappingEnvironmentVariableScopes_OutOfOrderDisposal_CorruptsSharedState()
    {
        // Arrange
        const string key = "UTC_PARALLEL_DEMO_VARIABLE";
        Environment.SetEnvironmentVariable(key, "original");

        // Act - simulates the interleaving two real parallel tests touching the same environment variable
        // could produce, deterministically and on one thread (not dependent on actual thread-scheduling
        // luck): two overlapping scopes, disposed out of nested (LIFO) order.
        ManageEnvironmentVariables testA = new ManageEnvironmentVariables(new Dictionary<string, string?> { [key] = "test-a-value" });
        ManageEnvironmentVariables testB = new ManageEnvironmentVariables(new Dictionary<string, string?> { [key] = "test-b-value" });

        testA.Dispose(); // "Test A" finishes first...

        string? valueWhileTestBStillActive = Environment.GetEnvironmentVariable(key);

        testB.Dispose();
        string? finalValue = Environment.GetEnvironmentVariable(key);

        // Assert
        using (new AssertionScope())
        {
            // WRONG - "Test B" is still supposed to be active here, but sees the pre-test value instead of "test-b-value"
            valueWhileTestBStillActive.Should().Be("original");

            // WRONG - should be back to "original" once both are done, but ended up as "test-a-value" instead
            finalValue.Should().Be("test-a-value");
        }

        // Cleanup
        Environment.SetEnvironmentVariable(key, null);
    }
}
