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

    [TestMethod]
    public void PrintEachElement_EmptyList_WritesNothingAndReturnsEmpty()
    {
        // Arrange
        var input = Enumerable.Empty<int>();
        using var writer = new StringWriter();
        var selector = (int x) => x.ToString();

        // Act
        var result = input.PrintEachElement(writer, selector).ToList();

        // Assert
        Assert.AreEqual(string.Empty, writer.ToString());
        Assert.IsFalse(result.Any());
    }

    [TestMethod]
    public void PrintEachElement_SingleItem_WritesItemAndReturnsItem()
    {
        // Arrange
        var input = new List<int> { 42 };
        using var writer = new StringWriter();
        var selector = (int x) => $"Item: {x}";
        var expectedOutput = "Item: 42" + Environment.NewLine;

        // Act
        var result = input.PrintEachElement(writer, selector).ToList();

        // Assert
        Assert.AreEqual(expectedOutput, writer.ToString());
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(42, result[0]);
    }

    [TestMethod]
    public void PrintEachElement_MultipleItems_WritesItemsAndReturnsItems()
    {
        // Arrange
        var input = new List<string> { "apple", "banana", "cherry" };
        using var writer = new StringWriter();
        var selector = (string s) => s.ToUpper();
        var expectedOutput = "APPLE" + System.Environment.NewLine +
                             "BANANA" + System.Environment.NewLine +
                             "CHERRY" + System.Environment.NewLine;

        // Act
        var result = input.PrintEachElement(writer, selector).ToList();

        // Assert
        Assert.AreEqual(expectedOutput, writer.ToString());
        Assert.AreEqual(3, result.Count);
        CollectionAssert.AreEqual(input, result);
    }
}
