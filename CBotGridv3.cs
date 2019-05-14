using System;
//To use Positions.Count() method
using System.Linq;
using cAlgo.API;

//This bot is compatible with cTrader 3.3
namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class CBotGridv3 : Robot
    {
        [Parameter("Maximum open buy position?", DefaultValue = 8, MinValue = 0)]
        public int MaxOpenBuy { get; set; }

        [Parameter("Maximum open Sell position?", DefaultValue = 8, MinValue = 0)]
        public int MaxOpenSell { get; set; }

        [Parameter("Pip step", DefaultValue = 10, MinValue = 1)]
        public int PipStep { get; set; }

        [Parameter("Stop loss pips", DefaultValue = 100, MinValue = 10, Step = 10)]
        public double StopLossPips { get; set; }

        [Parameter("First order volume", DefaultValue = 1000, MinValue = 1, Step = 1)]
        public double FirstVolume { get; set; }

        [Parameter("Max spread allowed to open position", DefaultValue = 3.0)]
        public double MaxSpread { get; set; }

        [Parameter("Target profit for each group of trade", DefaultValue = 3, MinValue = 1)]
        public int AverageTakeProfit { get; set; }

        //[Parameter("Debug flag, set to No on real account to avoid closing all positions when stoping this cBot", DefaultValue = false)]
        [Parameter("Debug No, no closing on cBot stop", DefaultValue = false)]
        public bool IfCloseAllPositionsOnStop { get; set; }

        [Parameter("Volume exponent", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0)]
        public double VolumeExponent { get; set; }

        private string ThiscBotLabel;
        private DateTime LastBuyTradeTime;
        private DateTime LastSellTradeTime;

        // cBot initialization
        protected override void OnStart()
        {
            // Set position label to cBot name
            ThiscBotLabel = this.GetType().Name;
            // Normalize volume in case a wrong volume was entered
            if (FirstVolume != (FirstVolume = Symbol.NormalizeVolumeInUnits(FirstVolume)))
            {
                Print("Volume entered incorrectly, volume has been changed to ", FirstVolume);
            }
        }

        // Error handling
        protected override void OnError(Error error)
        {
            Print("Error occured, error code: ", error.Code);
        }

        protected override void OnTick()
        {
            // Close all buy positions if all buy positions' target profit is met
            if (Positions.Count(x => x.TradeType == TradeType.Buy && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) > 0)
            {
                if (Positions.Where(x => x.TradeType == TradeType.Buy && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel).Average(x => x.NetProfit) >= FirstVolume * AverageTakeProfit * Symbol.PipSize)
                {
                    foreach (var position in Positions)
                    {
                        if (position.TradeType == TradeType.Buy && position.SymbolCode == Symbol.Code && position.Label == ThiscBotLabel)
                            ClosePosition(position);
                    }
                }
            }
            // Close all sell positions if all sell positions' target profit is met
            if (Positions.Count(x => x.TradeType == TradeType.Sell && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) > 0)
            {
                if (Positions.Where(x => x.TradeType == TradeType.Sell && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel).Average(x => x.NetProfit) >= FirstVolume * AverageTakeProfit * Symbol.PipSize)
                {
                    foreach (var position in Positions)
                    {
                        if (position.TradeType == TradeType.Sell && position.SymbolCode == Symbol.Code && position.Label == ThiscBotLabel)
                            ClosePosition(position);
                    }
                }
            }
            // Conditions check before process trade
            if (Symbol.Spread / Symbol.PipSize <= MaxSpread)
            {
                if (Positions.Count(x => x.TradeType == TradeType.Buy && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) < MaxOpenBuy)
                    ProcessBuy();
                if (Positions.Count(x => x.TradeType == TradeType.Sell && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) < MaxOpenSell)
                    ProcessSell();
            }

            if (!this.IsBacktesting)
                DisplayStatusOnChart();
        }

        protected override void OnStop()
        {
            Chart.RemoveAllObjects();
            // Close all open positions opened by this cBot on stop
            if (this.IsBacktesting || IfCloseAllPositionsOnStop)
            {
                foreach (var position in Positions)
                {
                    if (position.SymbolCode == Symbol.Code && position.Label == ThiscBotLabel)
                        ClosePosition(position);
                }
            }
        }

        private void ProcessBuy()
        {
            if (Positions.Count(x => x.TradeType == TradeType.Buy && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) == 0 && MarketSeries.Close.Last(1) > MarketSeries.Close.Last(2))
            {
                ExecuteMarketOrder(TradeType.Buy, Symbol, FirstVolume, ThiscBotLabel, StopLossPips, null);
                LastBuyTradeTime = MarketSeries.OpenTime.Last(0);
            }
            if (Positions.Count(x => x.TradeType == TradeType.Buy && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) > 0)
            {
                if (Symbol.Ask < (Positions.Where(x => x.TradeType == TradeType.Buy && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel).Min(x => x.EntryPrice) - PipStep * Symbol.PipSize) && LastBuyTradeTime != MarketSeries.OpenTime.Last(0))
                {
                    ExecuteMarketOrder(TradeType.Buy, Symbol, CalculateVolume(TradeType.Buy), ThiscBotLabel, StopLossPips, null);
                    LastBuyTradeTime = MarketSeries.OpenTime.Last(0);
                }
            }
        }

        private void ProcessSell()
        {
            if (Positions.Count(x => x.TradeType == TradeType.Sell && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) == 0 && MarketSeries.Close.Last(2) > MarketSeries.Close.Last(1))
            {
                ExecuteMarketOrder(TradeType.Sell, Symbol, FirstVolume, ThiscBotLabel, StopLossPips, null);
                LastSellTradeTime = MarketSeries.OpenTime.Last(0);
            }
            if (Positions.Count(x => x.TradeType == TradeType.Sell && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) > 0)
            {
                if (Symbol.Bid > (Positions.Where(x => x.TradeType == TradeType.Sell && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel).Max(x => x.EntryPrice) + PipStep * Symbol.PipSize) && LastSellTradeTime != MarketSeries.OpenTime.Last(0))
                {
                    ExecuteMarketOrder(TradeType.Sell, Symbol, CalculateVolume(TradeType.Sell), ThiscBotLabel, StopLossPips, null);
                    LastSellTradeTime = MarketSeries.OpenTime.Last(0);
                }
            }
        }

        private double CalculateVolume(TradeType tradeType)
        {
            return Symbol.NormalizeVolumeInUnits(FirstVolume * Math.Pow(VolumeExponent, Positions.Count(x => x.TradeType == tradeType && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel)));
        }

        private void DisplayStatusOnChart()
        {
            if (Positions.Count(x => x.TradeType == TradeType.Buy && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) > 1)
            {
                var y = Positions.Where(x => x.TradeType == TradeType.Buy && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel).Average(x => x.EntryPrice);
                Chart.DrawHorizontalLine("bpoint", y, Color.Yellow, 2, LineStyle.Dots);
            }
            else
                Chart.RemoveObject("bpoint");
            if (Positions.Count(x => x.TradeType == TradeType.Sell && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) > 1)
            {
                var z = Positions.Where(x => x.TradeType == TradeType.Sell && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel).Average(x => x.EntryPrice);
                Chart.DrawHorizontalLine("spoint", z, Color.HotPink, 2, LineStyle.Dots);
            }
            else
                Chart.RemoveObject("spoint");
            Chart.DrawStaticText("pan", GenerateStatusText(), VerticalAlignment.Top, HorizontalAlignment.Left, Color.Tomato);
        }

        private string GenerateStatusText()
        {
            var statusText = "";
            var buyPositions = "";
            var sellPositions = "";
            var spread = "";
            var buyDistance = "";
            var sellDistance = "";
            spread = "\nSpread = " + Math.Round(Symbol.Spread / Symbol.PipSize, 1);
            buyPositions = "\nBuy Positions = " + Positions.Count(x => x.TradeType == TradeType.Buy && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel);
            sellPositions = "\nSell Positions = " + Positions.Count(x => x.TradeType == TradeType.Sell && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel);
            if (Positions.Count(x => x.TradeType == TradeType.Buy && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) > 0)
            {
                var averageBuyFromCurrent = Math.Round((Positions.Where(x => x.TradeType == TradeType.Buy && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel).Average(x => x.EntryPrice) - Symbol.Bid) / Symbol.PipSize, 1);
                buyDistance = "\nBuy Target Away = " + averageBuyFromCurrent;
            }
            if (Positions.Count(x => x.TradeType == TradeType.Sell && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel) > 0)
            {
                var averageSellFromCurrent = Math.Round((Symbol.Ask - Positions.Where(x => x.TradeType == TradeType.Sell && x.SymbolCode == Symbol.Code && x.Label == ThiscBotLabel).Average(x => x.EntryPrice)) / Symbol.PipSize, 1);
                sellDistance = "\nSell Target Away = " + averageSellFromCurrent;
            }
            if (Symbol.Spread / Symbol.PipSize > MaxSpread)
                statusText = "MAX SPREAD EXCEED";
            else
                statusText = ThiscBotLabel + buyPositions + spread + sellPositions + buyDistance + sellDistance;
            return (statusText);
        }
    }
}
