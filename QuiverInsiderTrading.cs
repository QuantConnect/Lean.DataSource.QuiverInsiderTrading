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

using Newtonsoft.Json;
using NodaTime;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Orders;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace QuantConnect.DataSource
{
    /// <summary>
    /// Insider Trading by private businesses
    /// </summary>
    [JsonObject]
    public class QuiverInsiderTrading : BaseDataCollection
    {
        private static readonly TimeSpan _period = TimeSpan.FromDays(1);

        /// <summary>
        /// Name
        /// </summary>
        [JsonProperty(PropertyName = "Name")]
        public string Name { get; set; }

        /// <summary>
        /// Shares amount in transaction
        /// </summary>
        [JsonProperty(PropertyName = "Shares")]
        public decimal? Shares { get; set; }

        /// <summary>
        /// PricePerShare of transaction
        /// </summary>
        [JsonProperty(PropertyName = "PricePerShare")]
        public decimal? PricePerShare { get; set; }

        /// <summary>
        /// Shares Owned after transaction
        /// </summary>
        [JsonProperty(PropertyName = "SharesOwnedFollowing")]
        public decimal? SharesOwnedFollowing { get; set; }

        /// <summary>
        /// The type of transaction
        /// </summary>
        public OrderDirection Transaction { get; set; } = OrderDirection.Hold;

        /// <summary>
        /// The date the transaction took place
        /// </summary>
        [JsonProperty(PropertyName = "Date")]
        [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd")]
        public DateTime Date { get; set; }

        /// <summary>
        /// The time the data point ends at and becomes available to the algorithm
        /// </summary>
        public override DateTime EndTime => Time + _period;

        /// <summary>
        /// Return the URL string source of the file. This will be converted to a stream
        /// </summary>
        /// <param name="config">Configuration object</param>
        /// <param name="date">Date of this source file</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>String URL of source file.</returns>
        public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            return new SubscriptionDataSource(
                Path.Combine(
                    Globals.DataFolder,
                    "alternative",
                    "quiver",
                    "insidertrading",
                    $"{config.Symbol.Value.ToLowerInvariant()}.csv"
                ),
                SubscriptionTransportMedium.LocalFile,
                FileFormat.FoldingCollection
            );
        }

        /// <summary>
        /// Parses the data from the line provided and loads it into LEAN
        /// </summary>
        /// <param name="config">Subscription configuration</param>
        /// <param name="line">Line of data</param>
        /// <param name="date">Date</param>
        /// <param name="isLiveMode">Is live mode</param>
        /// <returns>New instance</returns>
        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            var csv = line.Split(',');
            var price = csv[5].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture));

            return new QuiverInsiderTrading
            {
                Symbol = config.Symbol,
                Time = Parse.DateTimeExact(csv[0], "yyyyMMdd"),
                Value = price ?? 0,
                Date = Parse.DateTimeExact(csv[1], "yyyyMMdd"),
                Name = csv[2],
                Transaction = (OrderDirection)Enum.Parse(typeof(OrderDirection), csv[3]),
                Shares = csv[4].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture)),
                PricePerShare = price,
                SharesOwnedFollowing = csv[6].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture))
            };
        }

        /// <summary>
        /// Converts the instance to string
        /// </summary>
        public override string ToString()
        {
            if (Data.Count > 0)
            {
                // we are the wrapper instance
                return $"{Symbol} - Data Points {Data.Count}";
            }
            return $"{Symbol} - {Date:yyyyMMdd} - {Name} - {Shares} - {PricePerShare} - {SharesOwnedFollowing} - {Transaction}";
        }

        /// <summary>
        /// Indicates whether the data source is tied to an underlying symbol and requires that corporate events be applied to it as well, such as renames and delistings
        /// </summary>
        /// <returns>false</returns>
        public override bool RequiresMapping()
        {
            return true;
        }

        /// <summary>
        /// Clone implementation
        /// </summary>
        public override BaseData Clone()
        {
            return new QuiverInsiderTrading()
            {
                Name = Name,
                Shares = Shares,
                PricePerShare = PricePerShare,
                SharesOwnedFollowing = SharesOwnedFollowing,
                Transaction = Transaction,
                Date = Date,
                Data = Data,
                Symbol = Symbol,
                Time = Time,
            };
        }

        /// <summary>
        /// Indicates whether the data is sparse.
        /// If true, we disable logging for missing files
        /// </summary>
        /// <returns>true</returns>
        public override bool IsSparseData()
        {
            return true;
        }

        /// <summary>
        /// Gets the default resolution for this data and security type
        /// </summary>
        public override Resolution DefaultResolution()
        {
            return Resolution.Daily;
        }

        /// <summary>
        /// Gets the supported resolution for this data and security type
        /// </summary>
        public override List<Resolution> SupportedResolutions()
        {
            return DailyResolution;
        }

        /// <summary>
        /// Specifies the data time zone for this data type. This is useful for custom data types
        /// </summary>
        /// <returns>The <see cref="T:NodaTime.DateTimeZone" /> of this data type</returns>
        public override DateTimeZone DataTimeZone()
        {
            return TimeZones.NewYork;
        }
    }
}
