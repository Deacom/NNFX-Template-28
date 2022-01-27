using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
using Newtonsoft.Json;
using TradeLib;
using TradeLib.Entities;

[assembly: AllowPartiallyTrustedCallers()]
namespace cAlgo.Robots
{
    //Set time zone to Eastern Standard Time EP9-Best time to trade
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.FileSystem)]
    public class MyNNFXTemplate28 : Robot
    {
        //Parameters for the Template Risk Management
        [Parameter("Risk %", Group = "Risk Management", DefaultValue = 2)]
        public int RiskPct { get; set; }

        [Parameter("SL Factor", Group = "Risk Management", DefaultValue = 1.5)]
        public double SlFactor { get; set; }

        [Parameter("TP Factor", Group = "Risk Management", DefaultValue = 1)]
        public double TpFactor { get; set; }

        //Parameters for the Template General Settings
        [Parameter("Trade On Time", Group = "General Settings", DefaultValue = false)]
        public bool TradeOnTime { get; set; }

        [Parameter("Trade On Multiple Instruments", Group = "General Settings", DefaultValue = false)]
        public bool TradeMultipleInstruments { get; set; }

        [Parameter("Name of WatchList", Group = "General Settings", DefaultValue = "28Pairs")]
        public string WatchListName { get; set; }

        [Parameter("Trade Hour", Group = "General Settings", DefaultValue = "16")]
        public int TradeHour { get; set; }

        [Parameter("Trade Minute", Group = "General Settings", DefaultValue = "55")]
        public int TradeMinute { get; set; }
        [Parameter("File Path", Group = "General Settings", DefaultValue = "D:\\ForexData\\Save.json")]
        public string FilePath { get; set; }



        [Parameter("MACD Long Cycle", Group = "MACD Settings", DefaultValue = 26)]
        public int MACDLongCylcle { get; set; }
        [Parameter("MACD Short Cycle", Group = "MACD Settings", DefaultValue = 12)]
        public int MACDShortCycle { get; set; }
        [Parameter("MACD Signal Periods", Group = "MACD Settings", DefaultValue = 9)]
        public int MACDSignalPeriods { get; set; }
        [Parameter("MACD Last Cross", Group = "MACD Settings", DefaultValue = 5)]
        public int CheckMACDLastCross { get; set; }


        [Parameter("SSL Period", Group = "SSL Settings", DefaultValue = 10)]
        public int SSLPeriod { get; set; }

        [Parameter("SSL MA Type", Group = "SSL Settings", DefaultValue = MovingAverageType.Exponential)]
        public MovingAverageType SSLMAType { get; set; }

        [Parameter("SSL Last Cross", Group = "SSL Settings", DefaultValue = 5)]
        public int CheckSSLLastCross { get; set; }


        [Parameter("Chaikin Period", Group = "Chaikin Settings", DefaultValue = 14)]
        public int CVPeriod { get; set; }


        [Parameter("Min AF", Group = "Correlation PSAR", DefaultValue = 0.02, MinValue = 0)]
        public double MinAF { get; set; }

        [Parameter("Max AF", Group = "Correlation PSAR", DefaultValue = 0.2, MinValue = 0)]
        public double MaxAF { get; set; }


        //indicator variables for the Template for multi symbols
        private readonly Dictionary<string, AverageTrueRange> _atrList = new Dictionary<string, AverageTrueRange>();
        private readonly Dictionary<string, TradeSymbolInfo> _tradeSymbolInfoList = new Dictionary<string, TradeSymbolInfo>();
        //private Dictionary<string, int> CurrencyRankingList = new Dictionary<string, int>();
        private readonly List<TradeSymbolInfo> _symbolsToTradeList = new List<TradeSymbolInfo>();


        private readonly Dictionary<string, MacdCrossOver> _MACDCrossOverList = new Dictionary<string, MacdCrossOver>();
        private readonly Dictionary<string, SSLChannel> _sslList = new Dictionary<string, SSLChannel>();
        private readonly Dictionary<string, ChaikinMoneyFlow> _cvList = new Dictionary<string, ChaikinMoneyFlow>();

        private Dictionary<string, ParabolicSAR> parabolicSARList = new Dictionary<string, ParabolicSAR>();
        private TimeFrame higherTimeframe;
        private Dictionary<string, int> CorrelationTable = new Dictionary<string, int>();

        private SSLChannel _ssl;
        private ChaikinMoneyFlow _cv;

        //indicator variables for the Template for single symbol
        private string _botName;
        private AverageTrueRange _atr;
        private int _barToCheck;
        private double riskPercentage;
        private bool _hadABigBar = false;
        private int Day { get; set; }



        //indicator variables for the Imported indicators
        private MacdCrossOver _MACDCrossOver;
        #region Ctrader EventHandlers
        protected override void OnStart()
        {
            _botName = GetType().ToString();
            _botName = _botName.Substring(_botName.LastIndexOf('.') + 1);
            switch (TimeFrame.Name)
            {
                case ("Hour"):
                    higherTimeframe = TimeFrame.Hour4;
                    break;
                case ("Hour4"):
                    higherTimeframe = TimeFrame.Daily;
                    break;
                case ("Daily"):
                    higherTimeframe = TimeFrame.Weekly;
                    break;
                default:
                    throw new Exception("Wrong Timeframe for this bot");
            }


            _barToCheck = TradeOnTime ? 0 : 1;
            riskPercentage = (double)RiskPct / 100;


            foreach (string symbolName in Watchlists.FirstOrDefault(w => w.Name == WatchListName).SymbolNames.ToArray())
            {
                TradeSymbolInfo tradeSymbolInfo = new TradeSymbolInfo 
                {
                    Symbol = Symbols.GetSymbol(symbolName)
                };


                _tradeSymbolInfoList.Add(symbolName, tradeSymbolInfo);
            }

            foreach (KeyValuePair<string, TradeSymbolInfo> symbol in _tradeSymbolInfoList)
            {
                var bars = MarketData.GetBars(TimeFrame, symbol.Key);

                parabolicSARList.Add(symbol.Key, Indicators.ParabolicSAR(MarketData.GetBars(higherTimeframe, symbol.Key), MinAF, MaxAF));

                var cur1 = symbol.Key.Substring(0, 3);
                var cur2 = symbol.Key.Substring(3, 3);
                if (!CorrelationTable.ContainsKey(cur1))
                {
                    CorrelationTable.Add(cur1, 0);
                }
                if (!CorrelationTable.ContainsKey(cur2))
                {
                    CorrelationTable.Add(cur2, 0);
                }

                if (!TradeOnTime)
                {
                    bars.BarOpened += OnBarsBarOpened;
                }
                else
                {
                    bars.Tick += OnBarTick;
                }
                _atrList.Add(symbol.Key, Indicators.AverageTrueRange(bars, 14, MovingAverageType.Exponential));

                //Load here the specific indicators for this bot for multiple Instruments
                _MACDCrossOverList.Add(symbol.Key, Indicators.MacdCrossOver(MACDLongCylcle, MACDShortCycle, MACDSignalPeriods));
                _sslList.Add(symbol.Key, Indicators.GetIndicator<SSLChannel>(bars, SSLPeriod, SSLMAType));
                _cvList.Add(symbol.Key, Indicators.ChaikinMoneyFlow(bars, CVPeriod));
            }

            if (TradeMultipleInstruments)
            {
                //if (File.Exists(FilePath))
                //{
                //    string json = File.ReadAllText(FilePath);
                //    CurrencyRankingList = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
                //}
            }
            else
            {
                _atr = Indicators.AverageTrueRange(14, MovingAverageType.Exponential);

                //Load here the specific indicators for this bot for a single instrument
                _MACDCrossOver = Indicators.MacdCrossOver(MACDLongCylcle, MACDShortCycle, MACDSignalPeriods);
                _ssl = Indicators.GetIndicator<SSLChannel>(SSLPeriod, SSLMAType);
                _cv = Indicators.ChaikinMoneyFlow(CVPeriod);
            }
            //foreach (string symbolName in Watchlists.FirstOrDefault(w => w.Name == WatchListName).SymbolNames.ToArray())
            //{
            //    var cur1 = symbolName.Substring(0, 3);
            //    var cur2 = symbolName.Substring(3, 3);
            //    if (!CurrencyRankingList.ContainsKey(cur1))
            //    {
            //        CurrencyRankingList.Add(cur1, 1);
            //    }
            //    if (!CurrencyRankingList.ContainsKey(cur2))
            //    {
            //        CurrencyRankingList.Add(cur2, 1);
            //    }
            //}
            Positions.Closed += PositionsOnClosed;
        }

        protected override void OnStop()
        {
            //if (!TradeMultipleInstruments)
            //{
            //    return;
            //}
            //string json = JsonConvert.SerializeObject(CurrencyRankingList);
            //File.WriteAllText(FilePath, json);
        }

        private void PositionsOnClosed(PositionClosedEventArgs obj)
        {
            if (obj.Reason == PositionCloseReason.TakeProfit)
            {
                Position position = Positions.Find(obj.Position.Label);
                if (position != null)
                {
                    if (position.TradeType == TradeType.Buy)
                    {
                        ModifyPosition(position, position.EntryPrice + obj.Position.Pips * Symbols.GetSymbol(obj.Position.SymbolName).PipSize / 3, null, true);
                    }

                    if (position.TradeType == TradeType.Sell)
                    {
                        ModifyPosition(position, position.EntryPrice + obj.Position.Pips * Symbols.GetSymbol(obj.Position.SymbolName).PipSize / 3, null, true);
                    }
                }

                //CurrencyRankingList[obj.Position.SymbolName.Substring(0, 3)] += 3;
                //CurrencyRankingList[obj.Position.SymbolName.Substring(3, 3)] += 3;
            }
            if (obj.Reason == PositionCloseReason.StopLoss)
            {
                //CurrencyRankingList[obj.Position.SymbolName.Substring(0, 3)] -= 1;
                //CurrencyRankingList[obj.Position.SymbolName.Substring(3, 3)] -= 1;
            }
        }

        private void OnBarTick(BarsTickEventArgs obj)
        {
            if (TradeMultipleInstruments && TradeOnTime && TimeToTrade())
            {
                TradeController(obj.Bars, _tradeSymbolInfoList[obj.Bars.SymbolName].Symbol, _atrList[obj.Bars.SymbolName]);
            }
        }

        private void OnBarsBarOpened(BarOpenedEventArgs obj)
        {


            if (TradeMultipleInstruments && !TradeOnTime)
            {
                TradeController(obj.Bars, _tradeSymbolInfoList[obj.Bars.SymbolName].Symbol, _atrList[obj.Bars.SymbolName]);
            }
        }

        protected override void OnTick()
        {
            if (!TradeMultipleInstruments && TradeOnTime && TimeToTrade())
            {
                TradeController(Bars, Symbol, _atr);
            }
        }

        protected override void OnBar()
        {
            if (Day != Server.Time.Day)
            {
                List<string> keys = CorrelationTable.Keys.ToList();
                foreach (string key in keys)
                {
                    CorrelationTable[key] = 0;
                }
                foreach (string symbolName in Watchlists.FirstOrDefault(w => w.Name == WatchListName).SymbolNames.ToArray())
                {
                    var bars = MarketData.GetBars(TimeFrame, symbolName);
                    CalculateCorrelationTable(bars);
                }
            }

            if (!TradeMultipleInstruments && !TradeOnTime)
            {
                TradeController(Bars, Symbol, _atr);
            }
        }
        #endregion
        private void TradeController(Bars bars, Symbol symbol, AverageTrueRange atr)
        {
            bool executeTrades = false;
            string label = string.Format("{0}_{1}", _botName, symbol.Name);

            CheckForTradesToClose(bars, symbol, label);

            if (bars.SymbolName == _tradeSymbolInfoList.Last().Key || !TradeMultipleInstruments)
            {
                executeTrades = true;
            }

            TradeType tradetype = CheckForTradesToOpen(bars, symbol, atr);
            if (tradetype != (TradeType)3)
            {
                _symbolsToTradeList.Add(new TradeSymbolInfo 
                {
                    Atr = atr,
                    Label = label,
                    Symbol = symbol,
                    TradeType = tradetype,
                    Risk = riskPercentage
                });
            }

            if (executeTrades)
            {
                List<TradeSymbolInfo> trades = (_symbolsToTradeList.Count > 1) ? DevideRiskTradeList(_symbolsToTradeList) : _symbolsToTradeList;
                foreach (TradeSymbolInfo trade in trades)
                {
                    if (trade.TradeType == TradeType.Buy)
                    {
                        Close(TradeType.Sell, trade.Symbol, trade.Label);
                        Open(trade.TradeType, trade.Symbol, trade.Atr, trade.Label, trade.Risk);
                    }
                    else if (trade.TradeType == TradeType.Sell)
                    {
                        Close(TradeType.Buy, trade.Symbol, trade.Label);
                        Open(trade.TradeType, trade.Symbol, trade.Atr, trade.Label, trade.Risk);
                    }
                }
                _symbolsToTradeList.Clear();
            }
        }

        //private void CleanRanking()
        //{
        //    CurrencyRankingList = CurrencyRankingList.OrderBy(t => t.Value).ToDictionary(x => x.Key, x => x.Value);
        //    return;
        //}

        //private List<TradeSymbolInfo> CleanAndSortList(List<TradeSymbolInfo> tradableSymbolList)
        //{
        //    List<TradeSymbolInfo> toTrade = new List<TradeSymbolInfo>();
        //    //foreach (TradeSymbolInfo tradeSymbol in tradableSymbolList)
        //    //{
        //    //    tradeSymbol.Ranking = CurrencyRankingList[tradeSymbol.Symbol.Name.Substring(0, 3)] + CurrencyRankingList[tradeSymbol.Symbol.Name.Substring(3, 3)];
        //    //}

        //    while (tradableSymbolList.Count != 0)
        //    {
        //        TradeSymbolInfo trade = tradableSymbolList.FirstOrDefault(t => t.Ranking == tradableSymbolList.Max(s => s.Ranking));
        //        toTrade.Add(trade);
        //        tradableSymbolList.Remove(trade);
        //        tradableSymbolList.RemoveAll(t => t.Symbol.Name.Contains(trade.Symbol.Name.Substring(0, 3)));
        //        tradableSymbolList.RemoveAll(t => t.Symbol.Name.Contains(trade.Symbol.Name.Substring(3, 3)));
        //    }

        //    return toTrade.OrderBy(s => s.Ranking).ToList();
        //}

        private List<TradeSymbolInfo> DevideRiskTradeList(List<TradeSymbolInfo> tradableSymbolList)
        {
            List<TradeSymbolInfo> exclude = new List<TradeSymbolInfo>();
            List<string> currencys = new List<string>();
            Dictionary<string, List<TradeSymbolInfo>> tradesPerCurrency = new Dictionary<string, List<TradeSymbolInfo>>();

            foreach (var item in tradableSymbolList)
            {
                var cur1 = item.Symbol.Name.Substring(0, 3);
                var cur2 = item.Symbol.Name.Substring(3, 3);
                if (!currencys.Contains(cur1))
                {
                    currencys.Add(cur1);
                }
                if (!currencys.Contains(cur2))
                {
                    currencys.Add(cur2);
                }
            }
            foreach (string currency in currencys)
            {
                tradesPerCurrency.Add(currency, tradableSymbolList.Where(t => t.Symbol.Name.Contains(currency)).ToList());
            }
            foreach (KeyValuePair<string, List<TradeSymbolInfo>> pair in tradesPerCurrency.OrderByDescending(key => key.Value.Count))
            {
                foreach (var item in pair.Value)
                {
                    if (currencys.Contains(item.Symbol.Name.Substring(0, 3)) || currencys.Contains(item.Symbol.Name.Substring(3, 3)))
                    {
                        var sym = tradableSymbolList.FirstOrDefault(t => t.Symbol.Name == item.Symbol.Name);
                        var newCalculatedRisk = Math.Round(riskPercentage / pair.Value.Count(), 4);
                        sym.Risk = sym.Risk < newCalculatedRisk ? sym.Risk : newCalculatedRisk;
                        if (currencys.Contains(item.Symbol.Name.Substring(0, 3)))
                        {
                            currencys.Remove(item.Symbol.Name.Substring(0, 3));
                        }
                        if (currencys.Contains(item.Symbol.Name.Substring(3, 3)))
                        {
                            currencys.Remove(item.Symbol.Name.Substring(3, 3));
                        }
                    }
                }
            }

            return tradableSymbolList;
        }

        private bool IsSSLCrossLessThen(Bars bars, int lastSSLCrossAcceptable)
        {
            SSLChannel ssl = TradeMultipleInstruments ? _sslList[bars.SymbolName] : _ssl;

            for (int i = 0; i < lastSSLCrossAcceptable; i++)
            {
                double SSLUp = ssl.SslUp.Last(i);
                double PrevSSLUp = ssl.SslUp.Last(i + 1);
                double SSLDown = ssl.SslDown.Last(i);
                double PrevSSLDown = ssl.SslDown.Last(i + 1);

                if ((SSLUp < SSLDown && PrevSSLUp > PrevSSLDown) || (SSLUp > SSLDown && PrevSSLUp < PrevSSLDown))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsMACDCrossLessThen(Bars bars, int lastMACDCrossAcceptable)
        {
            MacdCrossOver macdCrossOver = TradeMultipleInstruments ? _MACDCrossOverList[bars.SymbolName] : _MACDCrossOver;

            for (int i = 0; i < lastMACDCrossAcceptable; i++)
            {
                double macd = macdCrossOver.MACD.Last(i);
                double Prevmacd = macdCrossOver.MACD.Last(i + 1);
                double macdsignal = macdCrossOver.Signal.Last(i);
                double Prevmacdsignal = macdCrossOver.Signal.Last(i + 1);

                if ((macd < macdsignal && Prevmacd > Prevmacdsignal) || (macd > macdsignal && Prevmacd < Prevmacdsignal))
                {
                    return true;
                }
            }
            return false;
        }
        private void CheckForTradesToClose(Bars bars, Symbol symbol, string label)
        {
            if (Server.Time.Date == DateTime.Parse("30/04/2020"))
            {

            }
            MacdCrossOver macdCrossOver = TradeMultipleInstruments ? _MACDCrossOverList[bars.SymbolName] : _MACDCrossOver;
            double macd = Math.Round(macdCrossOver.MACD.Last(1), 4);
            double Prevmacd = Math.Round(macdCrossOver.MACD.Last(2), 4);
            double macdsignal = Math.Round(macdCrossOver.Signal.Last(1), 4);
            double Prevmacdsignal = Math.Round(macdCrossOver.Signal.Last(2), 4);

            if (macd >= macdsignal && Prevmacd <= Prevmacdsignal)
            {
                if (Positions.FindAll(label, symbol.Name, TradeType.Sell) == null)
                {
                    return;
                }
                foreach (Position position in Positions.FindAll(label, symbol.Name, TradeType.Sell))
                {
                    ClosePosition(position);
                }
            }
            if (macd <= macdsignal && Prevmacd >= Prevmacdsignal)
            {
                if (Positions.FindAll(label, symbol.Name, TradeType.Buy) == null)
                {
                    return;
                }
                foreach (Position position in Positions.FindAll(label, symbol.Name, TradeType.Buy))
                {
                    ClosePosition(position);
                }
            }

        }

        private TradeType CheckForTradesToOpen(Bars bars, Symbol symbol, AverageTrueRange atr)
        {
            if (!IsSSLCrossLessThen(bars, CheckSSLLastCross))
            {
                return (TradeType)3;
            }
            if (!IsMACDCrossLessThen(bars, CheckMACDLastCross))
            {
                return (TradeType)3;
            }

            double barSize = Math.Round(Math.Abs((bars.HighPrices.Last(_barToCheck) - bars.LowPrices.Last(_barToCheck)) / symbol.PipSize), 0);
            double atrSize = Math.Round(atr.Result.Last(_barToCheck) / symbol.PipSize, 0);

            if (barSize > atrSize)
            {
                return (TradeType)3;
            }

            SSLChannel ssl = TradeMultipleInstruments ? _sslList[bars.SymbolName] : _ssl;
            MacdCrossOver macd = TradeMultipleInstruments ? _MACDCrossOverList[bars.SymbolName] : _MACDCrossOver;
            ChaikinMoneyFlow cv = TradeMultipleInstruments ? _cvList[bars.SymbolName] : _cv;

            double SSLUp = ssl.SslUp.Last(1);
            double SSLDown = ssl.SslDown.Last(1);
            double macdValue = Math.Round(macd.MACD.Last(_barToCheck), 4);
            double signal = Math.Round(macd.Signal.Last(_barToCheck), 4);
            double buffedSignal = signal + signal / 5;

            string currency1 = bars.SymbolName.Substring(0, 3);
            string currency2 = bars.SymbolName.Substring(3, 3);

            if (macdValue > buffedSignal && SSLUp > SSLDown && cv.Result.Last() > 0 && CorrelationTable[currency1] > CorrelationTable[currency2] && macd.Histogram.Last(1) < macd.Histogram.Last(2))
            {
                return TradeType.Buy;
            }

            else if (macdValue < buffedSignal && SSLUp < SSLDown && cv.Result.Last() > 0 && CorrelationTable[currency1] < CorrelationTable[currency2] && macd.Histogram.Last(1) > macd.Histogram.Last(2))
            {
                return TradeType.Sell;
            }

            return (TradeType)3;
        }

        //Function for opening a new trade
        private void Open(TradeType tradeType, Symbol symbol, AverageTrueRange atr, string label, double risk)
        {
            List<string> list = new List<string> 
            {
                symbol.Name
            };
            if (TradeMultipleInstruments)
            {
                list = Watchlists.FirstOrDefault(w => w.Name == WatchListName).SymbolNames.Where(s => s.Contains(symbol.Name.Substring(0, 3)) || s.Contains(symbol.Name.Substring(3, 3))).ToList();
            }

            //Check there's no existing position before entering a trade, label contains the Indicatorname and the currency
            foreach (var symbolname in list)
            {
                if (Positions.Find(label, symbolname, tradeType) != null)
                {
                    return;
                }
            }

            //var risk = 

            //Calculate trade amount based on ATR
            double atrSize = Math.Round(atr.Result.Last(_barToCheck) / symbol.PipSize, 0);
            double tradeAmount = Account.Equity * risk / (SlFactor * atrSize * symbol.PipValue);
            tradeAmount = symbol.NormalizeVolumeInUnits(tradeAmount / 2, RoundingMode.Down);

            ExecuteMarketOrder(tradeType, symbol.Name, tradeAmount, label, SlFactor * atrSize, TpFactor * atrSize);
            ExecuteMarketOrder(tradeType, symbol.Name, tradeAmount, label, SlFactor * atrSize, null);

            if (_hadABigBar)
            {
                _barToCheck = TradeOnTime ? 0 : 1;
                _hadABigBar = false;
            }
        }

        //Function for closing trades
        private void Close(TradeType tradeType, Symbol symbol, string label)
        {
            if (Positions.FindAll(label, symbol.Name, tradeType) == null)
            {
                return;
            }
            foreach (Position position in Positions.FindAll(label, symbol.Name, tradeType))
            {
                ClosePosition(position);
            }
        }

        private bool TimeToTrade()
        {
            return Server.Time.Hour == TradeHour && Server.Time.Minute == TradeMinute;
        }

        private void CalculateCorrelationTable(Bars bars)
        {
            var pSAR = parabolicSARList[bars.SymbolName];
            if (pSAR.Result.Last() > bars.Last().Close)
            {
                CorrelationTable[bars.SymbolName.Substring(0, 3)] += 1;
                CorrelationTable[bars.SymbolName.Substring(3, 3)] -= 1;
            }
            if (pSAR.Result.Last() < bars.Last().Close)
            {
                CorrelationTable[bars.SymbolName.Substring(0, 3)] -= 1;
                CorrelationTable[bars.SymbolName.Substring(3, 3)] += 1;
            }
        }
    }


    public class TradeSymbolInfo
    {
        public Symbol Symbol { get; set; }
        public AverageTrueRange Atr { get; set; }
        public TradeType TradeType { get; set; }
        public string Label { get; set; }
        public int Ranking { get; set; }
        public double Risk { get; set; }
    }
}
