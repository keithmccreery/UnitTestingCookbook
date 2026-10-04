# AutoFixture (and how it differs from Bogus)

## NuGet Packages Referenced

- AutoFixture https://github.com/AutoFixture/AutoFixture (MIT)
- AutoFixture.AutoMoq (MIT) - automatic Moq mocks for interfaces
- Bogus https://github.com/bchavez/Bogus - for the side-by-side comparison; see [Bogus](./README_Bogus.md) (the source of truth for Bogus)

All examples are located in `UnitTestingCookbook.Tests` -> [`AutoFixtureTests`](../UnitTestingCookbook.Tests/AutoFixtureTests.cs)  

**Versions and compatibility (checked 2026-10):**
- AutoFixture's latest **stable** release is **4.18.1 (November 2023)**. A 5.0 release candidate (5.0.0-rc.1, July
2026) shows the project is active, but this chapter sticks to the stable release.
- **`AutoFixture.NUnit4` (`[AutoData]`) doesn't support NUnit 5** - even the 5.0 RC declares `NUnit [4.0.0, 5.0.0)` -
and this repo is on NUnit 5. Core AutoFixture (`new Fixture()`) has no test-framework dependency, so that's what's
used here. (In xUnit or NUnit 4, `[AutoData]` lets AutoFixture supply a test method's parameters directly.)
- **`AutoFixture.AutoNSubstitute` 4.18.1 requires NSubstitute < 6** (this repo has 6.2.0), so the auto-mocking example
uses **`AutoFixture.AutoMoq`**, which supports Moq up to 5.0.  

---

## The short answer: two tools for two different questions

Both generate test data so you don't have to type it by hand - but they answer different questions:

- **Bogus: "What should this data *look like*?"** You describe each property with a rule (`RuleFor(x => x.Email, f =>
f.Internet.Email())`), and Bogus produces **realistic** values: real-looking names, emails, addresses, prices.
Anything you don't describe is left alone.
- **AutoFixture: "I don't care what this data is - just give me something valid-shaped."** With no rules at all, it
fills **every** property, constructor parameter, nested object, and collection with **anonymous** values -
`"Customer9e7e7ad9-bd6a-..."`, `85`, a random `Guid` - that are type-correct but deliberately meaningless.

Here is what each actually produced for this repo's types, with no hand-written data (real output from writing this
chapter):

| | AutoFixture, no setup | Bogus, no rules | Bogus, with rules |
|---|---|---|---|
| `Order.Customer` | `Customer9e7e7ad9-bd6a-4812-aeb4-f65bc42e554e` | `""` (empty) | a realistic "First Last" name (`f.Name.FullName()`) |
| `Order.Lines` | 3 lines, `Sku = "Sku117dd724-..."`, `Quantity = 85` | 0 lines | as many as the rule says |
| `Order.Id` | a random `Guid` | `Guid.Empty` | a random `Guid` (if there's a rule) |
| `NotificationOptions.FromEmail` | `FromEmail9d6cde34-...` - **not a valid email** | `null` | `Maryse20@gmail.com` |
| `NotificationOptions.MaxRetries` | `48` - **outside the valid 0-10** | `0` | `7` |

---

## What they have in common

- Both create populated objects so the **Arrange** step doesn't fill up with hand-typed literals.
- Both build nested objects and collections (Bogus with nested `Faker<T>`s, AutoFixture automatically).
- Both generate **different data on every run** by default - which is good for catching hidden assumptions, and a
problem if a test only passes for lucky values.
- Both let you override individual properties: Bogus with `RuleFor`, AutoFixture with `.Build<T>().With(...)`.
- **Neither one searches for failures or shrinks them.** That's [Property-Based Testing](./README_PropertyBasedTesting.md),
which generates inputs *specifically to break a rule* and then reduces a failure to its simplest form.

---

## AutoFixture fills everything, with no setup

<!-- snippet: AutoFixtureTests_A_AutoFixture_FillsEverything_WithNoSetup -->
<a id='snippet-AutoFixtureTests_A_AutoFixture_FillsEverything_WithNoSetup'></a>
```cs
public void A_AutoFixture_FillsEverything_WithNoSetup()
{
    // Arrange
    Fixture fixture = new Fixture();

    // Act - no rules, no configuration: every property, nested object, and list is filled in
    Order order = fixture.Create<Order>();

    // Assert - the values are "anonymous": present and type-correct, but deliberately meaningless
    // (e.g. Customer = "Customer9e7e7ad9-bd6a-...", Sku = "Sku117dd724-...", Quantity = 85)
    using (new AssertionScope())
    {
        order.Id.Should().NotBeEmpty();
        order.Customer.Should().StartWith("Customer"); // property name + a Guid
        order.Lines.Should().HaveCount(3); // collections get 3 items by default
        order.Lines.Should().AllSatisfy(line => line.Sku.Should().StartWith("Sku"));
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/AutoFixtureTests.cs#L33-L52' title='Snippet source file'>snippet source</a> | <a href='#snippet-AutoFixtureTests_A_AutoFixture_FillsEverything_WithNoSetup' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Bogus fills only what you describe

<!-- snippet: AutoFixtureTests_B_Bogus_FillsOnlyWhatYouDescribe -->
<a id='snippet-AutoFixtureTests_B_Bogus_FillsOnlyWhatYouDescribe'></a>
```cs
public void B_Bogus_FillsOnlyWhatYouDescribe()
{
    // Act - a Faker<T> with no rules just creates the object; it doesn't fill anything in
    Order noRules = new Faker<Order>().Generate();

    Order withRules = new Faker<Order>()
        .RuleFor(order => order.Id, faker => faker.Random.Guid())
        .RuleFor(order => order.Customer, faker => faker.Name.FullName())
        .RuleFor(order => order.Lines, faker => new Faker<OrderLine>()
            .RuleFor(line => line.Sku, f => f.Commerce.Ean8())
            .RuleFor(line => line.Quantity, f => f.Random.Int(1, 5))
            .RuleFor(line => line.UnitPrice, f => f.Finance.Amount(1, 100))
            .Generate(2))
        .Generate();

    // Assert
    using (new AssertionScope())
    {
        noRules.Customer.Should().BeEmpty();
        noRules.Lines.Should().BeEmpty();
        noRules.Id.Should().BeEmpty();

        withRules.Customer.Should().MatchRegex(@"^\S+ \S+"); // a realistic "First Last" name
        withRules.Lines.Should().HaveCount(2).And.AllSatisfy(line => line.Quantity.Should().BeInRange(1, 5));
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/AutoFixtureTests.cs#L59-L86' title='Snippet source file'>snippet source</a> | <a href='#snippet-AutoFixtureTests_B_Bogus_FillsOnlyWhatYouDescribe' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Anonymous data is not valid data

This is the difference that matters most in practice. `NotificationOptions` is validated by the MinimalApi project's
`NotificationOptionsValidator` (see [AppSettings Validation](./README_AppSettingsValidation.md)): `FromEmail` must be an
email address, and `MaxRetries` must be between 0 and 10. AutoFixture's anonymous values break both rules; Bogus's
realistic ones pass.

<!-- snippet: AutoFixtureTests_C_AnonymousData_IsNotValidData -->
<a id='snippet-AutoFixtureTests_C_AnonymousData_IsNotValidData'></a>
```cs
public void C_AnonymousData_IsNotValidData()
{
    // Arrange
    NotificationOptionsValidator validator = new NotificationOptionsValidator(); // FromEmail must be an email; MaxRetries 0-10

    // Act
    NotificationOptions anonymous = new Fixture().Create<NotificationOptions>();

    NotificationOptions realistic = new Faker<NotificationOptions>()
        .RuleFor(options => options.FromEmail, faker => faker.Internet.Email())
        .RuleFor(options => options.MaxRetries, faker => faker.Random.Int(0, 10))
        .Generate();

    // Assert
    using (new AssertionScope())
    {
        // e.g. FromEmail = "FromEmail9d6cde34-...", MaxRetries = 48
        ValidationResult anonymousResult = validator.Validate(anonymous);
        anonymousResult.IsValid.Should().BeFalse();
        anonymousResult.Errors.Select(error => error.PropertyName).Should().Contain(nameof(NotificationOptions.FromEmail));

        // e.g. FromEmail = "Maryse20@gmail.com", MaxRetries = 7
        validator.Validate(realistic).IsValid.Should().BeTrue();
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/AutoFixtureTests.cs#L93-L119' title='Snippet source file'>snippet source</a> | <a href='#snippet-AutoFixtureTests_C_AnonymousData_IsNotValidData' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

So if the code under test **validates, parses, or formats** its input, AutoFixture's defaults will usually trip it -
on properties the test doesn't even care about.

## Using both: AutoFixture for the rest, Bogus where it matters

They combine well. Let AutoFixture fill everything the test doesn't care about, and supply realistic values only for
the properties that have to look real:

<!-- snippet: AutoFixtureTests_D_Combined_AutoFixtureForTheRest_BogusWhereItMatters -->
<a id='snippet-AutoFixtureTests_D_Combined_AutoFixtureForTheRest_BogusWhereItMatters'></a>
```cs
public void D_Combined_AutoFixtureForTheRest_BogusWhereItMatters()
{
    // Arrange
    Fixture fixture = new Fixture();
    Faker faker = new Faker();

    // Act - .With() overrides just the properties this test cares about; AutoFixture fills the rest
    NotificationOptions options = fixture.Build<NotificationOptions>()
        .With(o => o.FromEmail, () => faker.Internet.Email())
        .With(o => o.MaxRetries, 3)
        .Create();

    // Assert
    new NotificationOptionsValidator().Validate(options).IsValid.Should().BeTrue();
}
```
<sup><a href='/UnitTestingCookbook.Tests/AutoFixtureTests.cs#L126-L142' title='Snippet source file'>snippet source</a> | <a href='#snippet-AutoFixtureTests_D_Combined_AutoFixtureForTheRest_BogusWhereItMatters' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Reproducibility: Bogus can be seeded, AutoFixture can't

A seeded Bogus `Faker` produces the **same** data every run (see [Bogus](./README_Bogus.md) for `UseSeed()`), so a
failure caused by a particular value can be reproduced. AutoFixture has no seed option: every run is different.

<!-- snippet: AutoFixtureTests_E_Reproducibility_BogusSeeds_AutoFixtureDoesNot -->
<a id='snippet-AutoFixtureTests_E_Reproducibility_BogusSeeds_AutoFixtureDoesNot'></a>
```cs
public void E_Reproducibility_BogusSeeds_AutoFixtureDoesNot()
{
    // Act
    string bogusRun1 = new Faker<NotificationOptions>().UseSeed(42).RuleFor(o => o.FromEmail, f => f.Internet.Email()).Generate().FromEmail;
    string bogusRun2 = new Faker<NotificationOptions>().UseSeed(42).RuleFor(o => o.FromEmail, f => f.Internet.Email()).Generate().FromEmail;

    string autoFixtureRun1 = new Fixture().Create<NotificationOptions>().FromEmail;
    string autoFixtureRun2 = new Fixture().Create<NotificationOptions>().FromEmail;

    // Assert
    using (new AssertionScope())
    {
        bogusRun1.Should().Be(bogusRun2); // same seed, same data - every run
        autoFixtureRun1.Should().NotBe(autoFixtureRun2); // AutoFixture has no seed: different every time
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/AutoFixtureTests.cs#L149-L166' title='Snippet source file'>snippet source</a> | <a href='#snippet-AutoFixtureTests_E_Reproducibility_BogusSeeds_AutoFixtureDoesNot' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Constructor-only types

AutoFixture creates objects through their **constructors** automatically, inventing every argument. Bogus's `RuleFor`
needs settable properties, so a constructor-only type (like the `FinanceTerms` record) needs an explicit
`CustomInstantiator`:

<!-- snippet: AutoFixtureTests_F_ConstructorOnlyTypes -->
<a id='snippet-AutoFixtureTests_F_ConstructorOnlyTypes'></a>
```cs
public void F_ConstructorOnlyTypes()
{
    // Act - AutoFixture calls the constructor itself, inventing a value for every parameter
    FinanceTerms fromAutoFixture = new Fixture().Create<FinanceTerms>();

    // Bogus's RuleFor needs settable properties, so a constructor-only record needs CustomInstantiator
    FinanceTerms fromBogus = new Faker<FinanceTerms>()
        .CustomInstantiator(faker => new FinanceTerms(faker.PickRandom("P", "L"), faker.PickRandom(24, 36, 48), faker.Random.Int(0, 5000)))
        .Generate();

    // Assert
    using (new AssertionScope())
    {
        fromAutoFixture.PaymentType.Should().StartWith("PaymentType"); // anonymous again
        fromBogus.PaymentType.Should().BeOneOf("P", "L"); // realistic, because we said what's realistic
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/AutoFixtureTests.cs#L173-L191' title='Snippet source file'>snippet source</a> | <a href='#snippet-AutoFixtureTests_F_ConstructorOnlyTypes' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Building the class under test, mocks and all (AutoMoq)

This is where AutoFixture does something Bogus doesn't attempt at all. With `AutoMoqCustomization`, AutoFixture
becomes an **auto-mocking container**: `fixture.Create<CachedProductPriceService>()` builds the service, creating a Moq
mock for every interface dependency it can't otherwise create. `Freeze` makes the test and the service share one mock
(so the test can set it up and verify it), and `Inject` supplies a real implementation where the test needs one.

The payoff: the test never spells out the constructor, so **adding a new constructor dependency later doesn't break
it**. The cost: a test that never mentions a dependency can quietly receive a mock whose methods return defaults
(`null`, `0`, `false`) - easy to miss when reading the test.

<!-- snippet: AutoFixtureTests_G_AutoMoq_BuildsTheSystemUnderTest -->
<a id='snippet-AutoFixtureTests_G_AutoMoq_BuildsTheSystemUnderTest'></a>
```cs
public async Task G_AutoMoq_BuildsTheSystemUnderTest()
{
    // Arrange - with AutoMoqCustomization, any interface AutoFixture can't otherwise create becomes a Moq mock
    Fixture fixture = new Fixture();
    fixture.Customize(new AutoMoqCustomization());

    // Freeze: "use this same instance everywhere one is needed" - so the test can set it up and verify it
    Mock<IProductPriceSource> source = fixture.Freeze<Mock<IProductPriceSource>>();
    source.Setup(s => s.GetPriceAsync("WIDGET", It.IsAny<CancellationToken>())).ReturnsAsync(9.99m);

    // A dependency this test needs to be real, not mocked
    fixture.Inject<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    // The constructor is never spelled out - adding a new constructor parameter later won't break this test
    CachedProductPriceService service = fixture.Create<CachedProductPriceService>();

    // Act
    await service.GetPriceAsync("WIDGET");
    decimal price = await service.GetPriceAsync("WIDGET");

    // Assert
    price.Should().Be(9.99m);
    source.Verify(s => s.GetPriceAsync("WIDGET", It.IsAny<CancellationToken>()), Times.Once());
}
```
<sup><a href='/UnitTestingCookbook.Tests/AutoFixtureTests.cs#L198-L223' title='Snippet source file'>snippet source</a> | <a href='#snippet-AutoFixtureTests_G_AutoMoq_BuildsTheSystemUnderTest' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`CachedProductPriceService` is the service from the [Caching](./README_Caching.md) chapter.

---

## Side by side

| | Bogus | AutoFixture |
|---|---|---|
| **Question it answers** | "What should this look like?" | "Give me *something* - I don't care what" |
| **Values** | Realistic (names, emails, prices, addresses) | Anonymous (`PropertyName` + `Guid`, small numbers) |
| **Setup** | A rule per property you want filled | None - fills everything by default |
| **Unfilled properties** | Left at their defaults | None - everything is filled |
| **Passes validation / parsing** | Yes, if the rules say so | Usually not, unless you override those properties |
| **Constructor-only types** | Needs `CustomInstantiator` | Automatic |
| **Mocks / building the class under test** | No | Yes, with AutoMoq (and AutoNSubstitute / AutoFakeItEasy) |
| **Reproducible (seed)** | Yes - `UseSeed()` | No |
| **Test-framework integration** | None needed | Optional `[AutoData]` (xUnit, NUnit 4 - not NUnit 5) |
| **Also useful outside tests** | Yes - demo data, database seeding, UI prototypes | Rarely |
| **Latest stable release** (checked 2026-10) | 35.6.5 (Oct 2025) | 4.18.1 (Nov 2023); 5.0 in release candidate |

### Pros and cons

**Bogus**
- **Pros:** data that looks real, so it passes validation and reads well in test output and failure messages; seedable
and reproducible; useful beyond tests; more recent stable releases (35.6.5, Oct 2025).
- **Cons:** every property you want filled needs a rule, and a type with many properties needs many rules; a new
property is silently left empty until someone adds a rule; constructor-only types need extra work; nothing for mocks.

**AutoFixture**
- **Pros:** zero setup for any type; tests keep compiling and passing when constructors or properties change; signals
clearly that "this value doesn't matter to this test"; can build the class under test with mocks (AutoMoq).
- **Cons:** anonymous data often fails validation, parsing, and formatting, forcing `.With(...)` overrides; values are
meaningless in failure messages; no seed, so a value-dependent failure is hard to reproduce; auto-mocking can hide
which dependencies a test really uses; no stable release since 2023 (5.0 in progress).

---

## Which should I use?

- **The test's data has to *look real*** (it's validated, parsed, displayed, or exported) - **Bogus**, with a rule for
each property that matters.
- **The test cares about one or two properties and the rest are noise** - **AutoFixture**, with `.Build<T>().With(...)`
for the ones that matter. Add Bogus inside `.With(...)` for any of those that must look real.
- **You want the class under test built for you, mocks included** - **AutoFixture + AutoMoq**.
- **You need the exact same data every run** (to reproduce a failure, or for snapshot tests) - **seeded Bogus**, or a
[Test Data Builder](./README_TestDataBuilders.md) with fixed values.
- **You want readers to see exactly what data the test uses** - a [Test Data Builder](./README_TestDataBuilders.md) or
the Prototype pattern: explicit and deterministic, at the cost of writing it.
- **You want to find the inputs that break a rule** - neither; that's
[Property-Based Testing](./README_PropertyBasedTesting.md).

**Other "auto-fill" tools:** AutoBogus combines the two ideas - Bogus's realistic data, filled in automatically
without per-property rules. The original `AutoBogus` package hasn't been released since July 2021, but there's an
actively maintained MIT fork, `Soenneker.Utils.AutoBogus` (checked 2026-10). Neither is used in this repo.  

---

Back to [README](../README.md)
