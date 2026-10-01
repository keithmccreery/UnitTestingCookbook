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
            csv.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{line.Sku},{line.Quantity},{line.UnitPrice:0.00},{line.Quantity * line.UnitPrice:0.00}"));
        }

        csv.AppendLine(string.Create(CultureInfo.InvariantCulture, $"TOTAL,,,{order.Total:0.00}"));

        return csv.ToString();
    }
}
