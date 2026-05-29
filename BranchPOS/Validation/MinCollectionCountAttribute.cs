using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace BranchPOS.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MinCollectionCountAttribute : ValidationAttribute
{
    private readonly int _minimumCount;

    public MinCollectionCountAttribute(int minimumCount)
    {
        _minimumCount = minimumCount;
        ErrorMessage = $"Collection must contain at least {minimumCount} item.";
    }

    public override bool IsValid(object? value)
    {
        if (value is not IEnumerable enumerable)
        {
            return false;
        }

        var count = 0;
        foreach (var _ in enumerable)
        {
            count++;
            if (count >= _minimumCount)
            {
                return true;
            }
        }

        return false;
    }
}
