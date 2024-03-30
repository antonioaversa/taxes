namespace Taxes.Test;

static class AssertExtensions
{
    public static void ThrowsAny<T>(Action action) where T : Exception
    {
        bool exceptionThrown;
        try
        {
            action();
            exceptionThrown = false;
        }
        catch (T)
        {
            exceptionThrown = true;
        }

        if (!exceptionThrown)
            Assert.Fail($"Expected exception of type {typeof(T)} or derived, but no exception was thrown");

    }
}
