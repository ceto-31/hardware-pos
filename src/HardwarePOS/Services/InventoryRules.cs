namespace HardwarePOS.Services;

public static class InventoryRules
{
    public static void ValidateQuantity(decimal quantity, bool allowsFractional = true)
    {
        if (quantity <= 0 || quantity > 999999999999999m || decimal.Round(quantity, 3) != quantity)
            throw new InvalidOperationException("Enter a positive quantity with at most 3 decimal places.");
        if (!allowsFractional && decimal.Truncate(quantity) != quantity)
            throw new InvalidOperationException("This product is sold in whole units only.");
    }

    public static bool IsReturnEligible(DateTime soldAt, DateTime now) => now >= soldAt && now <= soldAt.AddDays(7);

    // Cumulative rounding makes all line allocations sum exactly to the paid total.
    public static decimal[] AllocatePaidTotal(IReadOnlyList<decimal> lines, decimal paid)
    {
        var sum = lines.Sum();
        var result = new decimal[lines.Count];
        decimal cumulative = 0, allocated = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            cumulative += lines[i];
            var target = sum == 0 ? 0 : Math.Round(paid * cumulative / sum, 2, MidpointRounding.AwayFromZero);
            result[i] = target - allocated;
            allocated = target;
        }
        return result;
    }

    public static decimal Refund(decimal paidForLine, decimal sold, decimal alreadyReturned, decimal quantity, decimal alreadyRefunded)
    {
        ValidateQuantity(quantity);
        if (sold <= 0 || alreadyReturned + quantity > sold)
            throw new InvalidOperationException("Return quantity exceeds the remaining quantity on the receipt.");
        return Math.Max(0, Math.Round(paidForLine * (alreadyReturned + quantity) / sold, 2, MidpointRounding.AwayFromZero) - alreadyRefunded);
    }
}
