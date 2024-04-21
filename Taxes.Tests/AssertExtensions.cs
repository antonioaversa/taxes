namespace Taxes.Test;

static class AssertExtensions
{
    [AssertionMethod]
    public static void ThrowsAny<T>(Action action) where T : Exception
    {
        bool exceptionThrown = false;
        Exception? differentException = null;
        try
        {
            action();
            exceptionThrown = false;
        }
        catch (T)
        {
            exceptionThrown = true;
        }
        catch (Exception ex)
        {
            differentException = ex;
        }

        if (differentException is not null)
            Assert.Fail($"Expected exception of type {typeof(T)} or derived, but exception of type " +
                $"{differentException.GetType()} was thrown: {differentException}");

        if (!exceptionThrown)
            Assert.Fail($"Expected exception of type {typeof(T)} or derived, but no exception was thrown");
    }
}
