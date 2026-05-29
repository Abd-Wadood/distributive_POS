using System.Globalization;

namespace BranchPOS.Utilities;

public static class CurrencyFormatter
{
    private static readonly CultureInfo NumberCulture = CultureInfo.InvariantCulture;

    public static string Format(decimal amount) =>
        $"Rs. {amount.ToString("N2", NumberCulture)}";

    public static string Format(decimal? amount) =>
        amount.HasValue ? Format(amount.Value) : "-";
}
