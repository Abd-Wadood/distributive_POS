namespace BranchPOS.Services;

public sealed record InventoryUnitOption(
    string DisplayName,
    string CompatibleBaseUnit,
    decimal? DefaultConversionFactorToBase,
    bool RequiresCustomConversion)
{
    public bool IsFixedConversion => !RequiresCustomConversion;
}
