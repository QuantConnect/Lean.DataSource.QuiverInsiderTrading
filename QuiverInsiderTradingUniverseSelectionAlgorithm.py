# QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
# Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

from AlgorithmImports import *

### <summary>
### Example algorithm using the custom data type as a source of alpha
### </summary>
class QuiverInsiderTradingUniverseSelectionAlgorithm(QCAlgorithm):
    def Initialize(self):
        ''' Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized. '''

        # Data ADDED via universe selection is added with Daily resolution.
        self.UniverseSettings.Resolution = Resolution.Daily

        self.SetStartDate(2022, 2, 14)
        self.SetEndDate(2022, 2, 18)
        self.SetCash(100000)

        # add a custom universe data source (defaults to usa-equity)
        self.AddUniverse(QuiverInsiderTradingUniverse, "QuiverInsiderTradingUniverse", Resolution.Daily, self.UniverseSelection)

    def UniverseSelection(self, data):
        ''' Selected the securities
        
        :param List of QuiverInsiderTradingUniverse data: List of QuiverInsiderTradingUniverse
        :return: List of Symbol objects '''

        symbol_data = {}

        for datum in data:
            symbol = datum.Symbol
            self.Log(f"{symbol},{datum.Shares},{datum.PricePerShare},{datum.SharesOwnedFollowing}")
            
            if symbol not in symbol_data:
                symbol_data[symbol] = []
            symbol_data[symbol].append(datum)
        
        # define our selection criteria
        return [symbol for symbol, d in symbol_data.items()
                if len(d) >= 2 and sum([x.Shares * x.PricePerShare for x in d if x.Shares != None and x.PricePerShare != None]) > 100000]
        
    def OnSecuritiesChanged(self, changes):
        ''' Event fired each time that we add/remove securities from the data feed
		
        :param SecurityChanges changes: Security additions/removals for this time step
        '''
        self.Log(changes.ToString())