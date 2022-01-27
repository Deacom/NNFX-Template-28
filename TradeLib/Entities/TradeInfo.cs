using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeLib.Entities
{
    public class TradeInfo
    {
        public string Label { get; set; }
        public TradeType TradeType { get; set; }
        public Symbol Symbol { get; set; }
        public AverageTrueRange Atr { get; set; }
        public bool TradeMultipleInstruments { get; set; }
        public string WatchListName { get; set; }
        public int BarToCheck { get; set; }
        public double RiskPercentage { get; set; }
        public double TakeProfitFactor { get; set; }
        public double StopLossFactor { get; set; }
    }
}
