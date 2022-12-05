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
*/

using Newtonsoft.Json;
using QuantConnect.Configuration;
using QuantConnect.Data.Auxiliary;
using QuantConnect.DataSource;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Logging;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace QuantConnect.DataProcessing
{
    /// <summary>
    /// QuiverInsiderTradingDataDownloader implementation. https://www.quiverquant.com/
    /// </summary>
    public class QuiverInsiderTradingDataDownloader : IDisposable
    {
        public const string VendorName = "quiver";
        public const string VendorDataName = "insidertrading";
        
        private readonly string _destinationFolder;
        private readonly string _universeFolder;
        private readonly string _processedDataDirectory;
        private readonly bool _canCreateUniverseFiles;
        private readonly string _clientKey;
        private readonly int _maxRetries = 5;

        private readonly JsonSerializerSettings _jsonSerializerSettings = new ()
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        /// <summary>
        /// Control the rate of download per unit of time.
        /// </summary>
        private readonly RateGate _indexGate;

        /// <summary>
        /// Creates a new instance of <see cref="QuiverInsiderTrading"/>
        /// </summary>
        /// <param name="destinationFolder">The folder where the data will be saved</param>
        /// <param name="processedDataDirectory">The folder where the data will be read from</param>
        /// <param name="apiKey">The QuiverQuant API key</param>
        public QuiverInsiderTradingDataDownloader(string destinationFolder, string processedDataDirectory, string apiKey = null)
        {
            _destinationFolder = Path.Combine(destinationFolder, VendorName, VendorDataName);
            _universeFolder = Path.Combine(_destinationFolder, "universe");
            _processedDataDirectory = Path.Combine(processedDataDirectory, VendorName, VendorDataName);

            _clientKey = apiKey ?? Config.Get("vendor-auth-token");
            _canCreateUniverseFiles = Directory.Exists(Path.Combine(Globals.DataFolder, "equity", "usa", "map_files"));

            // Represents rate limits of 10 requests per 1.1 second
            _indexGate = new RateGate(1, TimeSpan.FromSeconds(2));

            Directory.CreateDirectory(_universeFolder);
        }

        /// <summary>
        /// Runs the instance of the object with a given date.
        /// </summary>
        /// <param name="processingStartDate">First date of data to be fetched and processed</param>
        /// <param name="processingEndDate">Last date of data to be fetched and processed</param>
        /// <returns>True if process last downloads successfully</returns>
        public bool Run(DateTime processingStartDate, DateTime processingEndDate)
        {
            var success = false;
            
            for (var processDate= processingStartDate; processDate<= processingEndDate; processDate = processDate.AddDays(1))
            {
                success = Run(processDate);
            }

            return success;
        }

        /// <summary>
        /// Runs the instance of the object with a given date.
        /// </summary>
        /// <param name="processDate">The date of data to be fetched and processed</param>
        /// <returns>True if process all downloads successfully</returns>
        public bool Run(DateTime processDate)
        {
            var stopwatch = Stopwatch.StartNew();
            Log.Trace($"QuiverInsiderTradingDataDownloader.Run(): Start downloading/processing QuiverQuant Insider Trading data");

            var today = DateTime.UtcNow.Date;
            try
            {
                if (processDate > today || processDate == DateTime.MinValue)
                {
                    Log.Trace($"Encountered data from invalid date: {processDate:yyyy-MM-dd} - Skipping");
                    return false;
                }
                
                var quiverInsiderTradingData = HttpRequester($"live/insiders?date={processDate:yyyyMMdd}").SynchronouslyAwaitTaskResult();
                if (string.IsNullOrWhiteSpace(quiverInsiderTradingData))
                {
                    // We've already logged inside HttpRequester
                    return false;
                }

                var insiderTradingByDate = JsonConvert.DeserializeObject<List<RawInsiderTrading>>(quiverInsiderTradingData, _jsonSerializerSettings);

                var insiderTradingByTicker = new Dictionary<string, List<string>>();
                var universeCsvContents = new List<string>();

                var mapFileProvider = new LocalZipMapFileProvider();
                mapFileProvider.Initialize(new DefaultDataProvider());

                foreach (var insiderTrade in insiderTradingByDate)
                {
                    var ticker = insiderTrade.Ticker;
                    if (ticker == null) continue;

                    ticker = ticker.Split(':').Last().Replace("\"", string.Empty).ToUpperInvariant().Trim();
                    var sid = SecurityIdentifier.GenerateEquity(ticker, Market.USA, true, mapFileProvider, processDate);

                    if (sid.Date == Time.BeginningOfTime) continue;

                    if (!insiderTradingByTicker.TryGetValue(ticker, out var _))
                    {
                        insiderTradingByTicker.Add(ticker, new List<string>());
                    }

                    var curRow = $"{insiderTrade.Name.Replace(",", string.Empty).Trim().ToLower()},{insiderTrade.Shares},{insiderTrade.PricePerShare},{insiderTrade.SharesOwnedFollowing}";
                    insiderTradingByTicker[ticker].Add($"{processDate:yyyyMMdd},{curRow}");

                    universeCsvContents.Add($"{sid},{ticker},{curRow}");
                }

                if (!_canCreateUniverseFiles)
                {
                    return false;
                }
                if (universeCsvContents.Any())
                {
                    SaveContentToFile(_universeFolder, $"{processDate:yyyyMMdd}", universeCsvContents);
                }

                insiderTradingByTicker.DoForEach(kvp => SaveContentToFile(_destinationFolder, kvp.Key, kvp.Value));
            }
            catch (Exception e)
            {
                Log.Error(e);
                return false;
            }

            Log.Trace($"QuiverInsiderTradingDataDownloader.Run(): Finished in {stopwatch.Elapsed.ToStringInvariant(null)}");
            return true;
        }

        /// <summary>
        /// Sends a GET request for the provided URL
        /// </summary>
        /// <param name="url">URL to send GET request for</param>
        /// <returns>Content as string</returns>
        /// <exception cref="Exception">Failed to get data after exceeding retries</exception>
        private async Task<string> HttpRequester(string url)
        {
            for (var retries = 1; retries <= _maxRetries; retries++)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        client.BaseAddress = new Uri("https://api.quiverquant.com/beta/");
                        client.DefaultRequestHeaders.Clear();

                        // You must supply your API key in the HTTP header,
                        // otherwise you will receive a 403 Forbidden response
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", _clientKey);

                        // Responses are in JSON: you need to specify the HTTP header Accept: application/json
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        // Makes sure we don't overrun Quiver rate limits accidentally
                        _indexGate.WaitToProceed();

                        var response = await client.GetAsync(Uri.EscapeUriString(url));
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            Log.Error($"QuiverInsiderTradingDataDownloader.HttpRequester(): Files not found at url: {Uri.EscapeUriString(url)}");
                            response.DisposeSafely();
                            return string.Empty;
                        }

                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            var finalRequestUri = response.RequestMessage.RequestUri; // contains the final location after following the redirect.
                            response = client.GetAsync(finalRequestUri).Result; // Reissue the request. The DefaultRequestHeaders configured on the client will be used, so we don't have to set them again.

                        }

                        response.EnsureSuccessStatusCode();

                        var result =  await response.Content.ReadAsStringAsync();
                        response.DisposeSafely();

                        return result;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, $"QuiverInsiderTradingDataDownloader.HttpRequester(): Error at HttpRequester. (retry {retries}/{_maxRetries})");
                    Thread.Sleep(1000);
                }
            }

            throw new Exception($"Request failed with no more retries remaining (retry {_maxRetries}/{_maxRetries})");
        }

        /// <summary>
        /// Saves contents to disk, deleting existing zip files
        /// </summary>
        /// <param name="destinationFolder">Final destination of the data</param>
        /// <param name="name">File name</param>
        /// <param name="contents">Contents to write</param>
        private void SaveContentToFile(string destinationFolder, string name, IEnumerable<string> contents)
        {
            var finalPath = Path.Combine(destinationFolder, $"{name.ToLowerInvariant()}.csv");
            string filePath;

            if (destinationFolder.Contains("universe"))
            {
                filePath = Path.Combine(_processedDataDirectory, "universe", $"{name}.csv");
            }
            else
            {
                filePath = Path.Combine(_processedDataDirectory, $"{name}.csv");
            }

            var finalFileExists = File.Exists(filePath);

            var lines = new HashSet<string>(contents);
            if (finalFileExists)
            {
                foreach (var line in File.ReadAllLines(filePath))
                {
                    lines.Add(line);
                }
            }

            var finalLines = destinationFolder.Contains("universe")
                ? lines.OrderBy(x => x)
                : lines.OrderBy(x => DateTime.ParseExact(x.Split(',').First(), "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));

            File.WriteAllLines(finalPath, finalLines);
        }

        private class RawInsiderTrading : QuiverInsiderTrading
        {
            /// <summary>
            /// The time the data point ends at and becomes available to the algorithm
            /// </summary>
            [JsonProperty(PropertyName = "Date")]
            [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd")]
            public DateTime Date { get; set; }
            
            /// <summary>
            /// The ticker/symbol for the company
            /// </summary>
            [JsonProperty(PropertyName = "Ticker")]
            public string Ticker { get; set; } = null!;
        }

        /// <summary>
        /// Disposes of unmanaged resources
        /// </summary>
        public void Dispose()
        {
            _indexGate?.Dispose();
        }
    }
}
