namespace Taxes.Tests
{
    [TestClass]
    public class FXRatesTest
    {
        // Nested class for testing DictionaryAlwaysReturning1
        [TestClass]
        public class DictionaryAlwaysReturning1Test
        {
            private static IDictionary<DateTime, decimal> GetInstance()
            {
                // Now we can directly instantiate it as it's internal and InternalsVisibleTo is set
                return new FxRates.DictionaryAlwaysReturning1(); 
            }

            [TestMethod]
            public void Indexer_ReturnsOne()
            {
                var dict = GetInstance();
                Assert.AreEqual(1.0m, dict[DateTime.Now]);
                Assert.AreEqual(1.0m, dict[DateTime.MinValue]);
                Assert.AreEqual(1.0m, dict[DateTime.MaxValue]);
            }

            [TestMethod]
            public void TryGetValue_ReturnsTrueAndOne()
            {
                var dict = GetInstance();
                bool result = dict.TryGetValue(DateTime.Now, out decimal value);
                Assert.IsTrue(result);
                Assert.AreEqual(1.0m, value);

                result = dict.TryGetValue(DateTime.UnixEpoch, out value);
                Assert.IsTrue(result);
                Assert.AreEqual(1.0m, value);
            }

            [TestMethod]
            public void ContainsKey_AlwaysReturnsTrue()
            {
                var dict = GetInstance();
                Assert.IsTrue(dict.ContainsKey(DateTime.Now));
                Assert.IsTrue(dict.ContainsKey(DateTime.MinValue));
            }

            [TestMethod]
            public void Contains_ReturnsTrueIfValueIsOne()
            {
                var dict = GetInstance();
                Assert.IsTrue(dict.Contains(new KeyValuePair<DateTime, decimal>(DateTime.Now, 1.0m)));
                Assert.IsFalse(dict.Contains(new KeyValuePair<DateTime, decimal>(DateTime.Now, 0.5m)));
                Assert.IsFalse(dict.Contains(new KeyValuePair<DateTime, decimal>(DateTime.UnixEpoch, 2.0m)));
            }

            [TestMethod]
            public void Indexer_Set_ThrowsNotSupportedException()
            {
                var dict = GetInstance();
                Assert.ThrowsException<NotSupportedException>(() => dict[DateTime.Now] = 2.0m);
            }

            [TestMethod]
            public void Add_KVP_ThrowsNotSupportedException()
            {
                var dict = GetInstance();
                Assert.ThrowsException<NotSupportedException>(() => dict.Add(new KeyValuePair<DateTime, decimal>(DateTime.Now, 2.0m)));
            }

            [TestMethod]
            public void Add_KeyAndValue_ThrowsNotSupportedException()
            {
                var dict = GetInstance();
                Assert.ThrowsException<NotSupportedException>(() => dict.Add(DateTime.Now, 2.0m));
            }

            [TestMethod]
            public void Clear_ThrowsNotSupportedException()
            {
                var dict = GetInstance();
                Assert.ThrowsException<NotSupportedException>(() => dict.Clear());
            }

            [TestMethod]
            public void Remove_Key_ThrowsNotSupportedException()
            {
                var dict = GetInstance();
                Assert.ThrowsException<NotSupportedException>(() => dict.Remove(DateTime.Now));
            }

            [TestMethod]
            public void Remove_KVP_ThrowsNotSupportedException()
            {
                var dict = GetInstance();
                Assert.ThrowsException<NotSupportedException>(() => dict.Remove(new KeyValuePair<DateTime, decimal>(DateTime.Now, 1.0m)));
            }

            [TestMethod]
            public void Keys_ThrowsNotSupportedException()
            {
                var dict = GetInstance();
                Assert.ThrowsException<NotSupportedException>(() => { var keys = dict.Keys; });
            }

            [TestMethod]
            public void Values_ThrowsNotSupportedException()
            {
                var dict = GetInstance();
                Assert.ThrowsException<NotSupportedException>(() => { var values = dict.Values; });
            }

            [TestMethod]
            public void CopyTo_ThrowsNotSupportedException()
            {
                var dict = GetInstance();
                var array = new KeyValuePair<DateTime, decimal>[1];
                Assert.ThrowsException<NotSupportedException>(() => dict.CopyTo(array, 0));
            }

            [TestMethod]
            public void GetEnumerator_ThrowsNotSupportedException()
            {
                var dict = GetInstance();
                Assert.ThrowsException<NotSupportedException>(() => dict.GetEnumerator());
            }

            [TestMethod]
            public void Count_ReturnsZero()
            {
                var dict = GetInstance();
                Assert.AreEqual(0, dict.Count);
            }

            [TestMethod]
            public void IsReadOnly_ReturnsTrue()
            {
                var dict = GetInstance();
                Assert.IsTrue(dict.IsReadOnly);
            }
        }
    }
} 