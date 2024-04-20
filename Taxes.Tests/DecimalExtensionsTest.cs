namespace Taxes.Test;

[TestClass]
public class DecimalExtensionsTest
{
    [TestMethod]
    public void R_PerformsRoundingViaBasics_WithTwoDigits()
    {
        var basics = new Basics() { Rounding = x => decimal.Round(x, 2) };
        Assert.AreEqual(1.23m, 1.2345m.R(basics));
        Assert.AreEqual(1.23m, 1.2349m.R(basics));
        Assert.AreEqual(1.24m, 1.2351m.R(basics));
        Assert.AreEqual(1.24m, 1.2355m.R(basics));
    }

    [TestMethod]
    public void R_PerformsRoundingViaBasics_WithThreeDigits()
    {
        var basics = new Basics() { Rounding = x => decimal.Round(x, 3) };
        Assert.AreEqual(1.234m, 1.2345m.R(basics));
        Assert.AreEqual(1.235m, 1.2349m.R(basics));
        Assert.AreEqual(1.235m, 1.23499m.R(basics));
        Assert.AreEqual(1.235m, 1.23545m.R(basics));
    }
}
