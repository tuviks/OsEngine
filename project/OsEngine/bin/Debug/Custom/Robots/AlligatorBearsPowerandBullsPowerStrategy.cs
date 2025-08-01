﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on intersection of the Alligator, Bears Power and Bulls Power Strategy

Buy:
1. Fast line (lips) above the middle line (teeth), medium above the slow line (jaw)
2. Bears Power columns should be below 0, but constantly growing
3. Bulls Power columns should be above 0 and grow - enter into a long position

Sell:
1. fast line (lips) below the midline (teeth), medium below the slow line (jaw)
2. Bulls Power columns should be above 0, but decrease
3. Bears Power columns should be below 0 and decrease - enter short position

Exit from the buy: the fast line is lower than the slow one
Exit from sell: fast line above slow line
 */

namespace OsEngine.Robots
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    [Bot("AlligatorBearsPowerandBullsPowerStrategy")]
    public class AlligatorBearsPowerandBullsPowerStrategy : BotPanel
    {
        // Reference to the main trading tab
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicators settings
        private StrategyParameterInt _alligatorFastLineLength;
        private StrategyParameterInt _alligatorMiddleLineLength;
        private StrategyParameterInt _alligatorSlowLineLength;
        private StrategyParameterInt _bearsPeriod;
        private StrategyParameterInt _bullsPeriod;

        // Indicators
        private Aindicator _alligator;
        private Aindicator _bullsPower;
        private Aindicator _bearsPower;

        // The last values of the indicators
        private decimal _lastFast;
        private decimal _lastMiddle;
        private decimal _lastSlow;
        private decimal _lastBears;
        private decimal _lastBulls;
        private decimal _prevBears;
        private decimal _prevBulls;

        public AlligatorBearsPowerandBullsPowerStrategy(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _alligatorFastLineLength = CreateParameter("Period Simple Moving Average Fast", 10, 10, 300, 10, "Indicator");
            _alligatorMiddleLineLength = CreateParameter("Period Simple Moving Middle", 20, 10, 300, 10, "Indicator");
            _alligatorSlowLineLength = CreateParameter("Period Simple Moving Slow", 30, 10, 300, 10, "Indicator");
            _bearsPeriod = CreateParameter("Bears Period", 20, 10, 300, 10, "Indicator");
            _bullsPeriod = CreateParameter("Bulls Period", 20, 10, 300, 10, "Indicator");

            // Create indicator Alligator
            _alligator = IndicatorsFactory.CreateIndicatorByName("Alligator", name + "Alligator", false);
            _alligator = (Aindicator)_tab.CreateCandleIndicator(_alligator, "Prime");
            ((IndicatorParameterInt)_alligator.Parameters[0]).ValueInt = _alligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_alligator.Parameters[1]).ValueInt = _alligatorMiddleLineLength.ValueInt;
            ((IndicatorParameterInt)_alligator.Parameters[2]).ValueInt = _alligatorFastLineLength.ValueInt;
            _alligator.Save();

            // Create indicator BullsPower
            _bullsPower = IndicatorsFactory.CreateIndicatorByName("BullsPower", name + "BullsPower", false);
            _bullsPower = (Aindicator)_tab.CreateCandleIndicator(_bullsPower, "NewArea0");
            ((IndicatorParameterInt)_bullsPower.Parameters[0]).ValueInt = _bullsPeriod.ValueInt;

            // Create indicator BearsPower
            _bearsPower = IndicatorsFactory.CreateIndicatorByName("BearsPower", name + "BearsPower", false);
            _bearsPower = (Aindicator)_tab.CreateCandleIndicator(_bearsPower, "NewArea1");
            ((IndicatorParameterInt)_bearsPower.Parameters[0]).ValueInt = _bearsPeriod.ValueInt;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += AlligatorBearsPowerandBullsPowerStrategy_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel129;
        }

        // Indicator Update event
        private void AlligatorBearsPowerandBullsPowerStrategy_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_alligator.Parameters[0]).ValueInt = _alligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_alligator.Parameters[1]).ValueInt = _alligatorMiddleLineLength.ValueInt;
            ((IndicatorParameterInt)_alligator.Parameters[2]).ValueInt = _alligatorFastLineLength.ValueInt;
            _alligator.Save();
            _alligator.Reload();

            ((IndicatorParameterInt)_bearsPower.Parameters[0]).ValueInt = _bearsPeriod.ValueInt;
            _bearsPower.Save();
            _bearsPower.Reload();

            ((IndicatorParameterInt)_bullsPower.Parameters[0]).ValueInt = _bullsPeriod.ValueInt;
            _bullsPower.Save();
            _bullsPower.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "AlligatorBearsPowerandBullsPowerStrategy";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _alligatorSlowLineLength.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (_startTradeTime.Value > _tab.TimeServerCurrent ||
                _endTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            // If there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // If the position closing mode, then exit the method
            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            // If there are no positions, then go to the position opening method
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // The last value of the indicators
                _lastFast = _alligator.DataSeries[2].Last;
                _lastMiddle = _alligator.DataSeries[1].Last;
                _lastSlow = _alligator.DataSeries[0].Last;
                _lastBulls = _bullsPower.DataSeries[0].Last;
                _lastBears = _bearsPower.DataSeries[0].Last;
                _prevBulls = _bullsPower.DataSeries[0].Values[_bullsPower.DataSeries[0].Values.Count - 2];
                _prevBears = _bearsPower.DataSeries[0].Values[_bearsPower.DataSeries[0].Values.Count - 2];

                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastFast > _lastMiddle && _lastMiddle > _lastSlow && _lastBears < 0 && _lastBears > _prevBears && _lastBulls > 0 && _lastBulls > _prevBulls)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastFast < _lastMiddle && _lastMiddle < _lastSlow && _lastBulls > 0 && _lastBulls < _prevBulls && _lastBears < 0 && _lastBears < _prevBears)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // The last value of the indicators
            _lastFast = _alligator.DataSeries[2].Last;
            _lastSlow = _alligator.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (_lastFast < _lastSlow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastFast > _lastSlow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
                    }
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }
    }
}