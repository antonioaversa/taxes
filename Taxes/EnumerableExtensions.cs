namespace Taxes;

static class EnumerableExtensions
{
    public static IEnumerable<T> EnsureNonEmpty<T>(this IEnumerable<T> values)
    {
        var enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
            throw new InvalidOperationException("Empty collection");
        yield return enumerator.Current;
        while (enumerator.MoveNext())
            yield return enumerator.Current;
    }
}
