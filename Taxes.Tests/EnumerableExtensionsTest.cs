using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Taxes.Tests;

[TestClass]
public class EnumerableExtensionsTest
{
    [TestMethod]
    public void EnsureNonEmpty_ReturnsInputWhenNonEmpty()
    {
        var values = new[] { 1, 2, 3 };
        var result = EnumerableExtensions.EnsureNonEmpty(values);
        CollectionAssert.AreEqual(values, result.ToList());
    }

    [TestMethod]
    public void EnsureNonEmpty_WhenInputIsEmpty_ThrowsException()
    {
        var values = Array.Empty<int>();
        Assert.ThrowsException<InvalidOperationException>(
            () => EnumerableExtensions.EnsureNonEmpty(values).ToList());

    }
}
