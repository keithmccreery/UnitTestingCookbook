using AutoFixture;
using AutoFixture.AutoMoq;

using Bogus;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Moq;

using UnitTestingCookbook.MinimalApi;
using UnitTestingCookbook.Support.Models;
using UnitTestingCookbook.Support.Services;

using ValidationResult = FluentValidation.Results.ValidationResult;

namespace UnitTestingCookbook.Tests;

//
// AutoFixture https://github.com/AutoFixture/AutoFixture, compared with Bogus - see README_AutoFixture.md
//
[Category("unit")]
[Category("autofixture")]
[TestFixture]
public class AutoFixtureTests
{
    //
    // Q: What does AutoFixture create with no setup at all?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AutoFixtureTests_A_AutoFixture_FillsEverything_WithNoSetup
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
    // end-snippet

    //
    // Q: What does Bogus create with no setup - and with rules?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AutoFixtureTests_B_Bogus_FillsOnlyWhatYouDescribe
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
    // end-snippet

    //
    // Q: Is anonymous data valid data?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AutoFixtureTests_C_AnonymousData_IsNotValidData
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
    // end-snippet

    //
    // Q: Can I use both - AutoFixture for the parts I don't care about, Bogus for the parts that must look real?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AutoFixtureTests_D_Combined_AutoFixtureForTheRest_BogusWhereItMatters
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
    // end-snippet

    //
    // Q: Can I reproduce the exact same generated data on the next run?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AutoFixtureTests_E_Reproducibility_BogusSeeds_AutoFixtureDoesNot
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
    // end-snippet

    //
    // Q: How do I create a type that only has a constructor (no settable properties)?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AutoFixtureTests_F_ConstructorOnlyTypes
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
    // end-snippet

    //
    // Q: Can AutoFixture build the class under test, mocks and all?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AutoFixtureTests_G_AutoMoq_BuildsTheSystemUnderTest
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
    // end-snippet
}
