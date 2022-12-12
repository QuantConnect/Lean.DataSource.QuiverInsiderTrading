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
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.DataProcessing;
using QuantConnect.DataSource;

namespace QuantConnect.DataLibrary.Tests
{
    [TestFixture]
    public class QuiverInsiderTradingTests
    {
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
        [TestCase("CRUA CRUB", ExpectedResult = new string[] {"CRUA", "CRUB"})]
        [TestCase("AAPL", ExpectedResult = new string[] {"AAPL"})]
        public string[] TryNormalizeDefunctTicker(string rawTicker)
        {
            var testDownloader = new TestDownloader();
            return testDownloader.TestTryNormalizeDefunctTicker(rawTicker);
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