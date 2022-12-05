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

using System;
using QuantConnect.Configuration;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.DataProcessing
{
    /// <summary>
    /// Entrypoint for the data downloader/converter
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entrypoint of the program
        /// </summary>
        /// <returns>Exit code. 0 equals successful, and any other value indicates the downloader/converter failed.</returns>
        public static void Main()
        {
            Config.Set("processing-start-date", "20140101");

            // Get the config values first before running. These values are set for us
            // automatically to the value set on the website when defining this data type
            var destinationDirectory = System.IO.Path.Combine(
                Config.Get("temp-output-directory", "/temp-output-directory"),
                "alternative");
            var processedDataDirectory = System.IO.Path.Combine(
                Config.Get("processed-data-directory", Globals.DataFolder),
                "alternative");
            processedDataDirectory = destinationDirectory;

            var processingEndDateValue = Config.Get("processing-end-date", Environment.GetEnvironmentVariable("QC_DATAFLEET_DEPLOYMENT_DATE"));
            processingEndDateValue ??= DateTime.Today.ToString("yyyyMMdd");
            var processingEndDate = Parse.DateTimeExact(processingEndDateValue, "yyyyMMdd");

            var processingStartDateValue = Config.Get("processing-start-date", processingEndDateValue);
            var processingStartDate = Parse.DateTimeExact(processingStartDateValue, "yyyyMMdd");

            QuiverInsiderTradingDataDownloader instance = null;
            try
            {
                // Pass in the values we got from the configuration into the downloader/converter.
                instance = new QuiverInsiderTradingDataDownloader(destinationDirectory, processedDataDirectory);
            }
            catch (Exception err)
            {
                Log.Error(err, $"QuantConnect.DataProcessing.Program.Main(): The downloader/converter for {QuiverInsiderTradingDataDownloader.VendorDataName} {QuiverInsiderTradingDataDownloader.VendorDataName} data failed to be constructed");
                Environment.Exit(1);
            }

            // No need to edit anything below here for most use cases.
            // The downloader/converter is ran and cleaned up for you safely here.
            try
            {
                // Run the data downloader/converter.
                var success = instance.Run(processingStartDate, processingEndDate);
                if (!success)
                {
                    Log.Error($"QuantConnect.DataProcessing.Program.Main(): Failed to download/process {QuiverInsiderTradingDataDownloader.VendorName} {QuiverInsiderTradingDataDownloader.VendorDataName} data");
                    Environment.Exit(1);
                }
            }
            catch (Exception err)
            {
                Log.Error(err, $"The downloader/converter for {QuiverInsiderTradingDataDownloader.VendorDataName} {QuiverInsiderTradingDataDownloader.VendorDataName} data exited unexpectedly");
                Environment.Exit(1);
            }
            finally
            {
                // Run cleanup of the downloader/converter once it has finished or crashed.
                instance.DisposeSafely();
            }
            
            // The downloader/converter was successful
            Environment.Exit(0);
        }
    }
}
