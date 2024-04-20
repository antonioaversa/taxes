namespace Taxes;

public static class DecimalExtensions
{
    public static decimal R(this decimal value, Basics basics) => basics.Rounding(value);
}