using System.Globalization;
using System.Text;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Support.Services;

public static class OrderCsvExporter
{
    public static string ToCsv(Order order)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("Sku,Quantity,UnitPrice,LineTotal");

        foreach (OrderLine line in order.Lines)
        {
            csv.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Escape(line.Sku)},{line.Quantity},{line.UnitPrice:0.00},{line.Quantity * line.UnitPrice:0.00}"));
        }

        csv.AppendLine(string.Create(CultureInfo.InvariantCulture, $"TOTAL,,,{order.Total:0.00}"));

        return csv.ToString();
    }

    // RFC 4180: a field containing a comma, double quote, or line break must be wrapped in double quotes, with any
    // double quote inside it doubled. (Found by PropertyBasedTestingTests.F_Csv_SkusRoundTrip.)
    private static string Escape(string field) =>
        field.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : field;
}
