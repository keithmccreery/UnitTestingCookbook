# General Tips

## NuGet Packages Referenced

- Ben.Demystifier https://github.com/benaadams/Ben.Demystifier

All examples are located in `UnitTestingCookbook.Tests` -> [`GeneralTipsTests`](../UnitTestingCookbook.Tests/GeneralTipsTests.cs)  

---

## How do I Capture Console to test?

Capturing `System.Console.*` requires redirecting Standard Output.  

### Answer 1 - Long Way

```csharp
public void A_CaptureConsole()
{
    using (StringWriter capturedConsole = new StringWriter())
    {
        TextWriter originalOutput = System.Console.Out;

        try
        {
            System.Console.SetOut(capturedConsole);

            // Arrange
            string expected = "Hello World" + System.Environment.NewLine;

            // Act
            System.Console.WriteLine("Hello World");

            string result = capturedConsole.ToString();

            // Assert
            result.Should().Be(expected);
        }
        finally
        {
            System.Console.SetOut(originalOutput);
        }
    }
}
```

### Answer 2 - Helper Class

This solution uses `ManageCaptureConsole` class, located in `UnitTestingCookbook.TestHelpers` project.  

```csharp
public void A_CaptureConsole_TestHelpers()
{
    // Arrange
    using ManageCaptureConsole capturedConsole = new ManageCaptureConsole();

    string expected = "Hello World" + System.Environment.NewLine;

    // Act
    System.Console.WriteLine("Hello World");

    string? result = capturedConsole.ToString();

    // Assert
    result.Should().Be(expected);
}
```

---

## How do I change ENVIRONMENT variables for testing?

### Answer 1 - Long Way

```csharp
public void B_EnvironmentVariables()
{
    const string ASPNETCORE_ENVIRONMENT = "ASPNETCORE_ENVIRONMENT";
    const string AWS_DEFAULT_REGION = "AWS_DEFAULT_REGION";

    Dictionary<string, string?> originalValues = new Dictionary<string, string?>();

    try
    {
        // Arrange
        originalValues[ASPNETCORE_ENVIRONMENT] = Environment.GetEnvironmentVariable(ASPNETCORE_ENVIRONMENT);
        originalValues[AWS_DEFAULT_REGION] = Environment.GetEnvironmentVariable(AWS_DEFAULT_REGION);

        // Act
        Environment.SetEnvironmentVariable(ASPNETCORE_ENVIRONMENT, "QA");
        Environment.SetEnvironmentVariable(AWS_DEFAULT_REGION, "us-east-1");

        const bool result = true; // Do some work

        // Assert
        result.Should().BeTrue(); // Check the work
    }
    finally
    {
        // If value is null, the environment variable will be deleted.
        originalValues
            .ToList()
            .ForEach(kvp => Environment.SetEnvironmentVariable(kvp.Key, kvp.Value));
    }
}
```

### Answer 2 - Helper Class

This solution uses `ManageEnvironmentVariables` class, located in `UnitTestingCookbook.TestHelpers` project.  

```csharp
public void B_EnvironmentVariables_TestHelpers()
{
    // Arrange
    Dictionary<string, string?> environmentVariables = new Dictionary<string, string?>()
    {
        { "ASPNETCORE_ENVIRONMENT", "QA" },
        { "AWS_DEFAULT_REGION", "us-east-1" }
    };

    using ManageEnvironmentVariables manageEnvironmentVariables = new ManageEnvironmentVariables(environmentVariables);

    // Act
    const bool result = true; // Do some work

    // Assert
    result.Should().BeTrue(); // Check the work
}
```

---

## How do I access a private Property?

**CAUTION:** This section and the two that follow (private Field, private Method) use reflection to reach into a
class's private implementation. This is useful as a last resort (e.g. testing legacy code you can't safely change
the shape of), but reaching for it often is usually a sign the class needs a seam - a constructor parameter, an
extracted interface, an internal member exposed via `InternalsVisibleTo` (see
[Internal Constructor](#how-do-i-access-an-internal-constructor-for-unit-testing) below) - rather than a test that
reaches past the public API.  

Sometimes it is necessary to access a Private Property.  

```csharp
public void C_Private_Property()
{
    // Arrange
    Miscellaneous miscellaneous = new Miscellaneous();

    // Act
    string? result = miscellaneous.GetPropertyValue<string>("PrivateProperty");

    // Assert
    result.Should().Be("private_property");
}
```

This solution uses an Extension Method `.GetPropertyValue<T>()`, located in `ReflectionExtensions.cs` in `UnitTestingCookbook.TestHelpers` project.  

```csharp
public static T? GetPropertyValue<T>(this object @this, string propertyName)
{
    ArgumentNullException.ThrowIfNull(@this);

    return (T?) (@this
        .GetType()
        .GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
        ?? throw new MissingMemberException(@this.GetType().Name, propertyName))
        .GetValue(@this, null);
}
```

---

## How do I access a private Field?

Sometimes it is necessary to access a Private Field. See below for an example.  

```csharp
public void D_Private_Field()
{
    // Arrange
    Miscellaneous miscellaneous = new Miscellaneous();

    // Act
    string? result = miscellaneous.GetFieldValue<string>("privateField");

    // Assert
    result.Should().Be("private_field");
}
```

This solution uses an Extension Method `.GetFieldValue<T>()`, located in `ReflectionExtensions.cs` in `UnitTestingCookbook.TestHelpers` project.  


```csharp
public static T? GetFieldValue<T>(this object @this, string fieldName)
{
    ArgumentNullException.ThrowIfNull(@this);

    return (T?) (@this
        .GetType()
        .GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
        ?? throw new MissingFieldException(@this.GetType().Name, fieldName))
        .GetValue(@this);
}
```

Example...  

When writing a Decorator that adds a `DelegatingHandler` to an `HttpClient`,
and you want to verify the handler was added and added in the correct order of the chain.
The Private Field `_handler` holds the chain of handlers.  

**NOTE:** Any time a `SocketsHttpHandler` is added, it should be the last item on the chain.  

```csharp
// Act
DelegatingHandler lifetimeTrackingHttpMessageHandler = httpClient.GetFieldValue<DelegatingHandler>("_handler");

List<DelegatingHandler> delegatingHandlers = new List<DelegatingHandler>
{
    lifetimeTrackingHttpMessageHandler
};
 
while (delegatingHandlers.Last() is not null
    && delegatingHandlers.Last().InnerHandler is DelegatingHandler delegateHandler)
{
    delegatingHandlers.Add(delegateHandler);
}

SocketsHttpHandler socketsHttpHandler = delegatingHandlers.Last().InnerHandler as SocketsHttpHandler;

// Assert
```

---

## How do I invoke a private Method?

Sometimes it is necessary to invoke a Private Method. 

```csharp
public void E_Private_Method()
{
    // Arrange
    Miscellaneous miscellaneous = new Miscellaneous();

    // Act
    bool result = miscellaneous.ExecuteMethod<bool>("PrivateMethod", "a message");

    // Assert
    result.Should().BeTrue();
}
```

This solution uses an Extension Method `.ExecuteMethod<T>()`, located in `ReflectionExtensions.cs` in `UnitTestingCookbook.TestHelpers` project.  

**NOTE:** DOES NOT HANDLE argument values as null - unable to .GetType() on null.

Please see this StackOverflow Question [GetMethod for generic method](https://stackoverflow.com/questions/4035719/getmethod-for-generic-method) on how to implement `.GetMethodExt()` to handle additional parameter types.  

```csharp
public static T? ExecuteMethod<T>(this object @this, string methodName, params object[] args)
{
    ArgumentNullException.ThrowIfNull(@this);

    return (T?) (@this
        .GetType()
        .GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
            args.Select(p => p.GetType()).ToArray())
        ?? throw new MissingMethodException(@this.GetType().Name, methodName))
        .Invoke(@this, args);
}
```

---

## How do I access an Internal Constructor (for unit testing)?

Sometimes it is necessary to instantiate a class for unit testing using an Internal Constructor. 

```csharp
public void F_Internal_Constructor()
{
    // Arrange

    // Act
    Miscellaneous miscellaneous = TestHelper.InstantiateInternalConstructor<Miscellaneous>("default_private_property_value");

    // Assert
    miscellaneous.Should().NotBeNull();
}
```

This solution uses a Helper Method `.InstantiateInternalConstructor<T>()`, located in `TestHelper.cs` in `UnitTestingCookbook.TestHelpers` project.  

**NOTE:** DOES NOT HANDLE argument values as null - unable to .GetType() on null.
Please see this StackOverflow Question [GetMethod for generic method](https://stackoverflow.com/questions/4035719/getmethod-for-generic-method) on how to implement `.GetMethodExt()` to handle additional parameter types.  


```csharp
public static T InstantiateInternalConstructor<T>(params object[] args)
{
    return (T) (typeof(T)
        .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, args.Select(p => p.GetType()).ToArray(), null)
        ?? throw new NotImplementedException("No internal constructor matches the parameters."))
        .Invoke(args);
}
```

---

## How do I access an Internal Class from another project?

File: `GlobalAttributes.cs` located in the `UnitTestingCookbook.Support` project.  

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleToAttribute("UnitTestingCookbook.Tests")]
```

---

## How do I Pretty Print a Stack Trace

```csharp
public void G_Demystifier()
{
    // Arrange

    // Act
    try
    {
        false.Should().BeTrue();
    }
    catch (Exception ex)
    {
        System.Console.WriteLine("Before...");
        System.Console.WriteLine(ex.ToString());

        ex.Demystify();
        System.Console.WriteLine();

        System.Console.WriteLine("After...");
        System.Console.WriteLine(ex.ToString());
    }

    // Assert
}
```

Output...

```text
Before...
NUnit.Framework.AssertionException: Expected boolean to be True, but found False.
   at AwesomeAssertions.Execution.LateBoundTestFramework.Throw(String message)
   at AwesomeAssertions.Primitives.BooleanAssertions`1.BeTrue(String because, Object[] becauseArgs)
   at UnitTestingCookbook.Tests.GeneralTipsTests.G_Demystifier() in C:\git\keithmccreery\UnitTestingCookbook\UnitTestingCookbook.Tests\GeneralTipsTests.cs:line 205

After...
NUnit.Framework.AssertionException: Expected boolean to be True, but found False.
   at void AwesomeAssertions.Execution.LateBoundTestFramework.Throw(string message)
   at AndConstraint<TAssertions> AwesomeAssertions.Primitives.BooleanAssertions<TAssertions>.BeTrue(string because, params object[] becauseArgs)
   at void UnitTestingCookbook.Tests.GeneralTipsTests.G_Demystifier() in C:/git/keithmccreery/UnitTestingCookbook/UnitTestingCookbook.Tests/GeneralTipsTests.cs:line 205
```

**NOTE:** AwesomeAssertions' internal call stack is noticeably shallower than FluentAssertions' was (no more
`TestFrameworkProvider`/`DefaultAssertionStrategy`/repeated `FailWith` frames), so `Demystify()` has less to
trim here than it used to - but the technique is unchanged.  

---

Back to [README](../README.md)
