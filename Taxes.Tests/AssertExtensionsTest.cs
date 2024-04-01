namespace Taxes.Test;

[TestClass]
public class AssertExtensionsTest
{
    [TestMethod]
    public void ThrowsAny_ThrowsWhenNoExceptionIsThrown() =>
        AssertExtensions.ThrowsAny<AssertFailedException>(() =>
            AssertExtensions.ThrowsAny<Exception>(() => { }));

    [TestMethod]
    public void ThrowsAny_ThrowsWhenDifferentExceptionIsThrown() => 
        AssertExtensions.ThrowsAny<AssertFailedException>(() =>
            AssertExtensions.ThrowsAny<ArgumentException>(() => throw new IOException()));

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
