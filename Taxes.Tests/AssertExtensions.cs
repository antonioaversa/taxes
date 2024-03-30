namespace Taxes.Test;

static class AssertExtensions
{
    public static void ThrowsAny<T>(Action action) where T : Exception
    {
        bool exceptionThrown = false;
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
            Assert.Fail($"Expected exception of type {typeof(T)} or derived, but exception of type {ex.GetType()} was thrown: {ex}");
        }

        if (!exceptionThrown)
            Assert.Fail($"Expected exception of type {typeof(T)} or derived, but no exception was thrown");
    }
}
