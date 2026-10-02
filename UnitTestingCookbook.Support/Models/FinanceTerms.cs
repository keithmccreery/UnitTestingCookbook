namespace UnitTestingCookbook.Support.Models;

// Immutable counterpart to Finance (see TestValues.cs) - used by the Test Data Builders chapter to show `with` expressions
public sealed record FinanceTerms(string PaymentType, int MaxTerms, int DownPayment);
