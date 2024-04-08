/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Linq;
using Newtonsoft.Json;
using NodaTime;
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.DataProcessing;
using QuantConnect.DataSource;
using QuantConnect.Orders;

namespace QuantConnect.DataLibrary.Tests
{
    [TestFixture]
    public class QuiverInsiderTradingTests
    {
        private readonly Symbol _symbol = new(SecurityIdentifier.Parse("AAPL R735QTJ8XC9X"), "AAPL");

        [Test]
        public void JsonRoundTrip()
        {
            var expected = CreateNewInstance();
            var type = expected.GetType();
            var serialized = JsonConvert.SerializeObject(expected);
            var result = JsonConvert.DeserializeObject(serialized, type);

            AssertAreEqual(expected, result);
        }

        [Test]
        public void Clone()
        {
            var expected = CreateNewInstance();
            var result = expected.Clone();

            AssertAreEqual(expected, result);
        }

        [TestCase("abc123:msft\"", ExpectedResult = new string[] {"MSFT"})]
        [TestCase("AAPL+", ExpectedResult = new string[] {"AAPL"})]
        [TestCase("AAPL-", ExpectedResult = new string[] {"AAPL"})]
        [TestCase("AAPL=", ExpectedResult = new string[] {"AAPL"})]
        [TestCase("GOOG|C", ExpectedResult = new string[] {"GOOG"})]
        [TestCase("A_", ExpectedResult = new string[] {"A"})]
        [TestCase("CRDA CRDB", ExpectedResult = new string[] {"CRDA", "CRDB"})]
        [TestCase("AAPL", ExpectedResult = new string[] {"AAPL"})]
        public string[] TryNormalizeDefunctTicker(string rawTicker)
        {
            var testDownloader = new TestDownloader();
            return testDownloader.TestTryNormalizeDefunctTicker(rawTicker);
        }

        [Test]
        public void TestReader()
        {
            const string content = "20230808,20230805,o'brien deirdre,0,31896.0,,168341.0";
            var instance = CreateNewInstance();
            var config = new SubscriptionDataConfig(typeof(QuiverInsiderTrading), _symbol, Resolution.Daily,
                DateTimeZone.Utc, DateTimeZone.Utc, false, false, false);
            var data = instance.Reader(config, content, DateTime.UtcNow, false) as QuiverInsiderTrading;

            Assert.IsNotNull(data);
            Assert.AreEqual(_symbol, data.Symbol);
            Assert.AreEqual(new DateTime(2023, 8, 8), data.Time);
            Assert.AreEqual(new DateTime(2023, 8, 5), data.Date);
            Assert.AreEqual("o'brien deirdre", data.Name);
            Assert.AreEqual(OrderDirection.Buy, data.Transaction);
            Assert.IsNull(data.PricePerShare);
            Assert.AreEqual(31896.0, data.Shares);
            Assert.AreEqual(168341.0, data.SharesOwnedFollowing);
        }

        [Test]
        public void TestUniverseReader()
        {
            const string content = "AAPL R735QTJ8XC9X,AAPL,20230805,o'brien deirdre,1,16477.0,181.99,151864.0";
            var instance = new QuiverInsiderTradingUniverse();
            var config = new SubscriptionDataConfig(typeof(QuiverInsiderTradingUniverse), Symbol.None, Resolution.Daily,
                DateTimeZone.Utc, DateTimeZone.Utc, false, false, false);
            var data = instance.Reader(config, content, DateTime.UtcNow, false) as QuiverInsiderTradingUniverse;

            Assert.IsNotNull(data);
            Assert.AreEqual(_symbol, data.Symbol);
            Assert.AreEqual(new DateTime(2023, 8, 5), data.Date);
            Assert.AreEqual("o'brien deirdre", data.Name);
            Assert.AreEqual(OrderDirection.Sell, data.Transaction);
            Assert.AreEqual(16477.0, data.Shares);
            Assert.AreEqual(181.99, data.PricePerShare);
            Assert.AreEqual(151864.0, data.SharesOwnedFollowing);
        }

        private void AssertAreEqual(object expected, object result, bool filterByCustomAttributes = false)
        {
            foreach (var propertyInfo in expected.GetType().GetProperties())
            {
                // we skip Symbol which isn't protobuffed
                if (filterByCustomAttributes && propertyInfo.CustomAttributes.Count() != 0)
                {
                    Assert.AreEqual(propertyInfo.GetValue(expected), propertyInfo.GetValue(result));
                }
            }
            foreach (var fieldInfo in expected.GetType().GetFields())
            {
                Assert.AreEqual(fieldInfo.GetValue(expected), fieldInfo.GetValue(result));
            }
        }

        private BaseData CreateNewInstance()
        {
            return new QuiverInsiderTrading
            {
                Symbol = Symbol.Empty,
                Time = DateTime.Today,
                Date = DateTime.Today.AddDays(-1),
                Transaction = OrderDirection.Buy,
                DataType = MarketDataType.Base,
                Name = "Institution name",
                Shares = 0.0m,
                PricePerShare = 0.0m,
                SharesOwnedFollowing = 0.0m
            };
        }

        public class TestDownloader : QuiverInsiderTradingDataDownloader
        {
            public TestDownloader()
                : base()
            {
            }

            public string[] TestTryNormalizeDefunctTicker(string rawTicker)
            {
                TryNormalizeDefunctTicker(rawTicker, out var tickerList);
                return tickerList;
            }
        }
    }
}