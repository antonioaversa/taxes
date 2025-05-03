using System.Collections;
using System.Runtime.InteropServices.JavaScript;

namespace Taxes;

/// <summary>
/// Defines the FX Rates by currency and day.
/// FX Rates are stored in a dictionary where:
/// - the key is the symbol of the currency (e.g. USD)
/// - and the value is another dictionary where:
///   - the key is the day (DateTime where only the date part is relevant)
///   - and the value is the FX Rate, for that currency, that day.
///
/// The FX Rates takes the Basics settings as input, in order to check for
/// the base currency every time an FX Rate is asked. If an FX Rate is asked
/// for the base currency, 1.0m is returned, irrespective of the date.
/// </summary>
public record FxRates(Basics Basics, Dictionary<string, Dictionary<DateTime, decimal>> Rates)
{
    public IDictionary<DateTime, decimal> this[string currency] => 
        currency == Basics.BaseCurrency 
            ? new DictionaryAlwaysReturning1() 
            : Rates.GetValueOrDefault(currency) ?? new Dictionary<DateTime, decimal>();

    public decimal? this[string currency, DateTime date]
    {
        get
        {
            if (currency == Basics.BaseCurrency)
                return 1.0m;
            if (!Rates.TryGetValue(currency, out var rates))
                return null;
            if (!rates.TryGetValue(date, out var result))
                return null;
            return result;
        }
    }

    private class DictionaryAlwaysReturning1 : IDictionary<DateTime, decimal>
    {
        public decimal this[DateTime key]
        {
            get => 1.0m;
            set => throw new NotSupportedException();
        }

        public ICollection<DateTime> Keys => throw new NotSupportedException();

        public ICollection<decimal> Values => throw new NotSupportedException();

        public int Count => 0;

        public bool IsReadOnly => true;

        public void Add(DateTime key, decimal value) => throw new NotSupportedException();

        public void Add(KeyValuePair<DateTime, decimal> item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(KeyValuePair<DateTime, decimal> item) => item.Value == 1.0m;

        public bool ContainsKey(DateTime key) => true;

        public void CopyTo(KeyValuePair<DateTime, decimal>[] array, int arrayIndex) => throw new NotSupportedException();

        public IEnumerator<KeyValuePair<DateTime, decimal>> GetEnumerator() => throw new NotSupportedException();

        public bool Remove(DateTime key) => throw new NotSupportedException();

        public bool Remove(KeyValuePair<DateTime, decimal> item) => throw new NotSupportedException();

        public bool TryGetValue(DateTime key, out decimal value)
        {
            value = 1.0m;
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}