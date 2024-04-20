namespace Taxes;

public static class DecimalExtensions
{
    /// <summary>
    /// Shortcut to round a decimal via the Rounding method defined in the provided Basics 
    /// </summary>
    public static decimal R(this decimal value, Basics basics) => basics.Rounding(value);
}