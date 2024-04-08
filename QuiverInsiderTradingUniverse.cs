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
using System.Globalization;
using System.IO;
using QuantConnect.Data;
using QuantConnect.Orders;
using static QuantConnect.StringExtensions;

namespace QuantConnect.DataSource
{
    /// <summary>
    /// Universe Selection helper class for QuiverQuant InsiderTrading dataset
    /// </summary>
    public class QuiverInsiderTradingUniverse : QuiverInsiderTrading
    {
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
            var price = csv[6].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture));
            
            return new QuiverInsiderTradingUniverse
            {
                Symbol = new Symbol(SecurityIdentifier.Parse(csv[0]), csv[1]),
                Time = date,
                Value = price ?? 0,
                Date = Parse.DateTimeExact(csv[2], "yyyyMMdd"),
                Name = csv[3],
                Transaction = (OrderDirection)Enum.Parse(typeof(OrderDirection), csv[4]),
                Shares = csv[5].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture)),
                PricePerShare = price,
                SharesOwnedFollowing = csv[7].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture))
            };
        }

        /// <summary>
        /// Converts the instance to string
        /// </summary>
        public override string ToString()
        {
            return Invariant($"{Symbol}({Time}) :: ") +
                   Invariant($"Date: {Date:yyyyMMdd} ") +
                   Invariant($"Name: {string.Join(';', Name)} ") +
                   Invariant($"Shares: {Shares} ") +
                   Invariant($"PricePerShare: {PricePerShare} ") +
                   Invariant($"SharesOwnedFollowing: {SharesOwnedFollowing}") +
                   Invariant($"Transaction: {Transaction}");
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
                Transaction = Transaction,
                Date = Date,
                Data = Data,
                Symbol = Symbol,
                Time = Time,
            };
        }
    }
}