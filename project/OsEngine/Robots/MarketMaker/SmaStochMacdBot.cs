// OsEngine Bot for trading SBER with SMA + Stochastic + MACD strategy + adaptive SL/TP + trailing stop + session time limit + trade frequency protection + alerts
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Windows;

namespace OsEngine.Robots.MarketMaker
{
    [Bot("SmaStochMacdBot")]
    public class SmaStochMacdBot : BotPanel
    {
        private BotTabSimple _tab;

        private MovingAverage _smaFast;
        private MovingAverage _smaSlow;
        private StochasticOscillator _stochastic;
        private MacdLine _macd;
        private AtrIndicator _atr;

        private DateTime _lastTradeTime = DateTime.MinValue;

        [BotParameter("Fast SMA Period", DefaultValue = 20)]
        public int SmaFastPeriod { get; set; }

        [BotParameter("Slow SMA Period", DefaultValue = 50)]
        public int SmaSlowPeriod { get; set; }

        [BotParameter("Stochastic Length", DefaultValue = 14)]
        public int StochLength { get; set; }

        [BotParameter("MACD Fast Length", DefaultValue = 12)]
        public int MacdFast { get; set; }

        [BotParameter("MACD Slow Length", DefaultValue = 26)]
        public int MacdSlow { get; set; }

        [BotParameter("MACD Signal Length", DefaultValue = 9)]
        public int MacdSignal { get; set; }

        [BotParameter("Volume", DefaultValue = 1)]
        public decimal Volume { get; set; }

        [BotParameter("Slippage", DefaultValue = 0.1)]
        public decimal Slippage { get; set; }

        [BotParameter("Stop Multiplier ATR", DefaultValue = 1.5)]
        public decimal StopAtrMultiplier { get; set; }

        [BotParameter("Profit Multiplier ATR", DefaultValue = 2.5)]
        public decimal ProfitAtrMultiplier { get; set; }

        [BotParameter("Trailing Stop ATR Multiplier", DefaultValue = 1.0)]
        public decimal TrailingStopAtrMultiplier { get; set; }

        [BotParameter("Trading Start Hour", DefaultValue = 10)]
        public int StartHour { get; set; }

        [BotParameter("Trading End Hour", DefaultValue = 18)]
        public int EndHour { get; set; }

        [BotParameter("Min Seconds Between Trades", DefaultValue = 300)]
        public int MinSecondsBetweenTrades { get; set; }

        public SberSmaStochMacdBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            _tab = CreateBotTab("Simple");

            _smaFast = new MovingAverage(name + "_FastSma", false) { Length = SmaFastPeriod, MovingAverageType = MovingAverageType.Simple };
            _smaSlow = new MovingAverage(name + "_SlowSma", false) { Length = SmaSlowPeriod, MovingAverageType = MovingAverageType.Simple };
            _stochastic = new StochasticOscillator(name + "_Stochastic", false) { Lenght = StochLength };
            _macd = new MacdLine(name + "_Macd", false) { FastLength = MacdFast, SlowLength = MacdSlow, SignalLength = MacdSignal };
            _atr = new AtrIndicator(name + "_Atr", false) { Length = 14 };

            _tab.IndicatorS.Add(_smaFast);
            _tab.IndicatorS.Add(_smaSlow);
            _tab.IndicatorS.Add(_stochastic);
            _tab.IndicatorS.Add(_macd);
            _tab.IndicatorS.Add(_atr);

            _tab.CandleFinishedEvent += OnCandleFinished;
        }

        private void OnCandleFinished(List<Candle> candles)
        {
            if (candles.Count < SmaSlowPeriod || _stochastic.ValuesD.Count < 2 || _macd.Macd.Count < 2 || _atr.Values.Count < 1)
                return;

            DateTime time = candles[^1].TimeStart;
            if (time.Hour < StartHour || time.Hour >= EndHour || (time - _lastTradeTime).TotalSeconds < MinSecondsBetweenTrades)
                return;

            var lastCandle = candles[^1];
            decimal price = lastCandle.Close;
            decimal atr = _atr.Values[^1];

            decimal smaFast = _smaFast.Values[^1];
            decimal smaSlow = _smaSlow.Values[^1];
            decimal stochK = _stochastic.ValuesK[^1];
            decimal stochD = _stochastic.ValuesD[^1];
            decimal macdValue = _macd.Macd[^1];
            decimal macdSignal = _macd.Signal[^1];

            if (_tab.PositionsOpenLong.Count == 0 &&
                smaFast > smaSlow &&
                price > smaFast &&
                stochK > stochD && stochK > 25 &&
                macdValue > macdSignal)
            {
                _tab.BuyAtMarket(Volume, Slippage);
                _tab.SetNewAlert("Buy signal executed at " + price);
                _lastTradeTime = time;
            }
            else if (_tab.PositionsOpenShort.Count == 0 &&
                     smaFast < smaSlow &&
                     price < smaFast &&
                     stochK < stochD && stochK < 80 &&
                     macdValue < macdSignal)
            {
                _tab.SellAtMarket(Volume, Slippage);
                _tab.SetNewAlert("Sell signal executed at " + price);
                _lastTradeTime = time;
            }

            foreach (var pos in _tab.PositionsOpenLong)
            {
                decimal stopPrice = pos.EntryPrice - atr * StopAtrMultiplier;
                decimal profitPrice = pos.EntryPrice + atr * ProfitAtrMultiplier;
                decimal trailingStop = price - atr * TrailingStopAtrMultiplier;

                if (price <= stopPrice || price >= profitPrice || stochK < stochD || trailingStop > pos.StopOrderPrice)
                    _tab.CloseAtMarket(pos, pos.OpenVolume);
            }

            foreach (var pos in _tab.PositionsOpenShort)
            {
                decimal stopPrice = pos.EntryPrice + atr * StopAtrMultiplier;
                decimal profitPrice = pos.EntryPrice - atr * ProfitAtrMultiplier;
                decimal trailingStop = price + atr * TrailingStopAtrMultiplier;

                if (price >= stopPrice || price <= profitPrice || stochK > stochD || trailingStop < pos.StopOrderPrice)
                    _tab.CloseAtMarket(pos, pos.OpenVolume);
            }
        }

        public override string GetNameStrategyType() => "SmaStochMacdBot";
        public override void ShowIndividualSettingsDialog() { }
    }
}
