namespace Taxes.Test;

[TestClass]
public class AssertExtensionsTests
{
    [TestMethod]
    [ExpectedException(typeof(AssertFailedException))]
    public void ThrowsAny_ThrowsWhenNoExceptionIsThrown()
    {
        AssertExtensions.ThrowsAny<Exception>(() => { });
    }

    [TestMethod]
    [ExpectedException(typeof(AssertFailedException))]
    public void ThrowsAny_ThrowsWhenDifferentExceptionIsThrown()
    {
        AssertExtensions.ThrowsAny<ArgumentException>(() => throw new IOException());
    }

    [TestMethod]
    public void ThrowsAny_DoesNotThrowWhenDerivedExceptionIsThrown()
    {
        AssertExtensions.ThrowsAny<Exception>(() => throw new InvalidOperationException());
    }

    [TestMethod]
    public void ThrowsAny_DoesNotThrowWhenExpectedExceptionIsThrown()
    {
        AssertExtensions.ThrowsAny<Exception>(() => throw new Exception());
    }
}
