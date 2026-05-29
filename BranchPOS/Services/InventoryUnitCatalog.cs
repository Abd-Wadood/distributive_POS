using BranchPOS.Exceptions;

namespace BranchPOS.Services;

public static class InventoryUnitCatalog
{
    public const string Gram = "Gram";
    public const string ML = "ML";
    public const string Piece = "Piece";

    private static readonly List<InventoryUnitOption> Options =
    [
        new("Gram", Gram, 1m, false),
        new("Kg", Gram, 1000m, false),
        new("5kg Bag", Gram, 5000m, false),
        new("10kg Bag", Gram, 10000m, false),
        new("20kg Bag", Gram, 20000m, false),
        new("25kg Bag", Gram, 25000m, false),
        new("50kg Bag", Gram, 50000m, false),

        new("ML", ML, 1m, false),
        new("Liter", ML, 1000m, false),
        new("1L Bottle", ML, 1000m, false),
        new("5L Can", ML, 5000m, false),
        new("10L Can", ML, 10000m, false),

        new("Piece", Piece, 1m, false),
        new("Dozen", Piece, 12m, false),
        new("Packet", Piece, null, true),
        new("Box", Piece, null, true),
        new("Carton", Piece, null, true),
        new("Crate", Piece, null, true),
        new("Bundle", Piece, null, true)
    ];

    public static IReadOnlyList<string> SupportedBaseUnits { get; } = [Gram, ML, Piece];

    public static IReadOnlyList<InventoryUnitOption> GetOptions() => Options;

    public static IReadOnlyList<InventoryUnitOption> GetOptionsForBaseUnit(string? baseUnit) =>
        Options
            .Where(x => string.Equals(x.CompatibleBaseUnit, NormalizeBaseUnit(baseUnit), StringComparison.OrdinalIgnoreCase))
            .ToList();

    public static InventoryUnitOption? FindOption(string? baseUnit, string? purchaseUnitName) =>
        Options.FirstOrDefault(x =>
            string.Equals(x.CompatibleBaseUnit, NormalizeBaseUnit(baseUnit), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.DisplayName, purchaseUnitName?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static decimal ValidateAndNormalize(string? baseUnit, string? purchaseUnitName, decimal? conversionFactorToBase)
    {
        var normalizedBaseUnit = NormalizeBaseUnit(baseUnit);
        if (!SupportedBaseUnits.Contains(normalizedBaseUnit))
        {
            throw new PosValidationException("Base unit must be Gram, ML, or Piece.");
        }

        if (string.IsNullOrWhiteSpace(purchaseUnitName))
        {
            throw new PosValidationException("Default purchase unit is required.");
        }

        var option = FindOption(normalizedBaseUnit, purchaseUnitName)
            ?? throw new PosValidationException($"Purchase unit {purchaseUnitName.Trim()} is not valid for base unit {normalizedBaseUnit}.");

        if (!conversionFactorToBase.HasValue || conversionFactorToBase.Value <= 0)
        {
            throw new PosValidationException("Default conversion factor must be greater than zero.");
        }

        if (option.IsFixedConversion && conversionFactorToBase.Value != option.DefaultConversionFactorToBase)
        {
            throw new PosValidationException($"{option.DisplayName} must convert to {option.DefaultConversionFactorToBase:0.###} {normalizedBaseUnit}.");
        }

        return option.IsFixedConversion ? option.DefaultConversionFactorToBase!.Value : conversionFactorToBase.Value;
    }

    public static string NormalizeBaseUnit(string? baseUnit)
    {
        var value = baseUnit?.Trim() ?? string.Empty;
        return SupportedBaseUnits.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)) ?? value;
    }
}
