namespace Taxes;

internal static class EnumerableExtensions
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
    
    public static IEnumerable<T> PrintEachElement<T>(this IEnumerable<T> values, TextWriter writer, Func<T, string> selector)
    {
        foreach (var value in values)
        {
            var formattedValue = selector(value);
            writer.WriteLine(formattedValue);
            yield return value;
        }
    }
}
