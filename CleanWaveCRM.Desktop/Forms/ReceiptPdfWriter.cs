using System.Globalization;
using System.Text;
using CRM.domain.Entities.Tenant;

namespace CleanWaveCRM.Desktop.Forms;

/// <summary>Writes printable text-based PDFs without external runtime dependencies.</summary>
internal static class ReceiptPdfWriter
{
    public static void Write(string path, Company company, LaundryOrder order, Payment payment)
    {
        var lines = new List<string>
        {
            company.ShopName,
            company.Address ?? string.Empty,
            company.ContactNumber is null ? string.Empty : $"Phone: {company.ContactNumber}",
            "",
            "PAYMENT RECEIPT",
            $"Receipt No: {payment.ReceiptNumber ?? $"RCPT-{order.Id:D6}"}",
            $"Order ID: {order.Id}",
            $"Payment Date: {(payment.PaidAt ?? DateTime.UtcNow).ToLocalTime():yyyy-MM-dd hh:mm tt}",
            $"Customer: {order.Customer?.FullName ?? "(unknown)"}",
            $"Customer Contact: {order.Customer?.ContactNumber ?? "-"}",
            "",
            "ITEMS"
        };

        lines.AddRange(order.Items.Select(item =>
            $"{item.ServiceName} | {item.Quantity.ToString("0.##", CultureInfo.InvariantCulture)} x {Money(item.UnitPrice)} = {Money(item.Subtotal)}"));
        lines.AddRange(order.AddOns.Select(addOn =>
            $"{addOn.InventoryItem?.Name ?? "Add-on"} | {addOn.Quantity.ToString("0.##", CultureInfo.InvariantCulture)} x {Money(addOn.UnitPrice)} = {Money(addOn.Subtotal)}"));
        lines.Add("");
        lines.Add($"Order Total: {Money(order.TotalAmount)}");
        if (payment.LoyaltyDiscount > 0) lines.Add($"Loyalty Discount: -{Money(payment.LoyaltyDiscount)}");
        lines.Add($"Amount Due: {Money(payment.AmountDue)}");
        lines.Add($"Amount Paid: {Money(payment.AmountPaid)}");
        lines.Add($"Balance: {Money(Math.Max(0m, payment.Balance))}");
        lines.Add($"Payment Method: {payment.Method}");
        lines.Add($"Status: {payment.Status}");
        lines.Add("");
        lines.Add("Thank you for your business.");

        WriteLines(path, lines);
    }

    public static void WriteLines(string path, IEnumerable<string> lines)
    {
        var pageLines = lines.SelectMany(WrapLine).Chunk(31).ToList();
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        AddObject(pdf, offsets, "<< /Type /Catalog /Pages 2 0 R >>");
        var pageObjectNumbers = Enumerable.Range(0, pageLines.Count).Select(i => 4 + i * 2).ToArray();
        AddObject(pdf, offsets, $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectNumbers.Select(n => $"{n} 0 R"))}] /Count {pageLines.Count} >>");
        AddObject(pdf, offsets, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        for (var pageIndex = 0; pageIndex < pageLines.Count; pageIndex++)
        {
            var pageNumber = pageObjectNumbers[pageIndex];
            var contentNumber = pageNumber + 1;
            AddObject(pdf, offsets, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentNumber} 0 R >>");

            var content = new StringBuilder("BT\n/F1 11 Tf\n");
            var y = 750;
            foreach (var line in pageLines[pageIndex])
            {
                content.Append("1 0 0 1 55 ").Append(y.ToString(CultureInfo.InvariantCulture)).Append(" Tm\n(")
                    .Append(EscapePdf(line)).Append(") Tj\n");
                y -= 22;
            }
            content.Append("ET");
            var stream = content.ToString();
            AddObject(pdf, offsets, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 ").Append(offsets.Count.ToString(CultureInfo.InvariantCulture)).Append("\n0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
            pdf.Append(offsets[i].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        pdf.Append("trailer\n<< /Size ").Append(offsets.Count.ToString(CultureInfo.InvariantCulture)).Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(xrefOffset.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF");

        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(pdf.ToString()));
    }

    private static void AddObject(StringBuilder pdf, ICollection<int> offsets, string body)
    {
        offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
        var index = offsets.Count - 1;
        pdf.Append(index.ToString(CultureInfo.InvariantCulture)).Append(" 0 obj\n")
            .Append(body).Append("\nendobj\n");
    }

    private static string Money(decimal value) => $"PHP {value:N2}";

    private static IEnumerable<string> WrapLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            yield return " ";
            yield break;
        }
        for (var i = 0; i < line.Length; i += 82)
            yield return line.Substring(i, Math.Min(82, line.Length - i));
    }

    private static string EscapePdf(string value)
    {
        var ascii = new string(value.Select(ch => ch is >= ' ' and <= '~' ? ch : '?').ToArray());
        return ascii.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}
