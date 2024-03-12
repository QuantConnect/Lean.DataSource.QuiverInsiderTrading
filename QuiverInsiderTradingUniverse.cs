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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NodaTime;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using static QuantConnect.StringExtensions;

namespace QuantConnect.DataSource
{
    /// <summary>
    /// Universe Selection helper class for QuiverQuant InsiderTrading dataset
    /// </summary>
    public class QuiverInsiderTradingUniverse : BaseDataCollection
    {
        private static readonly TimeSpan _period = TimeSpan.FromDays(1);

        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Shares amount in transaction
        /// </summary>
        public decimal? Shares { get; set; }

        /// <summary>
        /// PricePerShare of transaction
        /// </summary>
        public decimal? PricePerShare { get; set; }

        /// <summary>
        /// Shares Owned after transcation
        /// </summary>
        public decimal? SharesOwnedFollowing { get; set; }

        /// <summary>
        /// Time the data became available
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
                    "universe",
                    $"{date.ToStringInvariant(DateFormat.EightCharacter)}.csv"
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

            var shares = csv[3].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture));
            var price = csv[4].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture));
            var sharesAfter = csv[5].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture));
            
            return new QuiverInsiderTradingUniverse
            {
                Time = date,
                Name = csv[2],
                Shares = shares,
                PricePerShare = price,
                SharesOwnedFollowing = sharesAfter,

                Symbol = new Symbol(SecurityIdentifier.Parse(csv[0]), csv[1]),
                Value = price ?? 0
            };
        }

        /// <summary>
        /// Converts the instance to string
        /// </summary>
        public override string ToString()
        {
            return Invariant($"{Symbol}({Time}) :: ") +
                   Invariant($"Name: {string.Join(';', Name)} ") +
                   Invariant($"Shares: {Shares} ") +
                   Invariant($"PricePerShare: {PricePerShare} ") +
                   Invariant($"SharesOwnedFollowing: {SharesOwnedFollowing}");
        }

        /// <summary>
        /// Clone implementation
        /// </summary>
        public override BaseData Clone()
        {
            return new QuiverInsiderTradingUniverse()
            {
                Name = Name,
                Shares = Shares,
                PricePerShare = PricePerShare,
                SharesOwnedFollowing = SharesOwnedFollowing,
                Data = Data,
                Symbol = Symbol,
                Time = Time,
            };
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
            return TimeZones.Chicago;
        }
    }
}