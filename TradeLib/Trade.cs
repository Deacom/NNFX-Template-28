using cAlgo.API;
using cAlgo.Indicators;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeLib.Entities;

namespace TradeLib
{
    public class Trade : Robot
    {
        public void Open(TradeInfo tradeInfo)
        {
            List<string> list = new List<string>() { tradeInfo.Symbol.Name };
            if (tradeInfo.TradeMultipleInstruments)
            {
                list = Watchlists.FirstOrDefault(w => w.Name == tradeInfo.WatchListName).SymbolNames
                    .Where(s => 
                    s.Contains(tradeInfo.Symbol.Name.Substring(0, 3)) || 
                    s.Contains(tradeInfo.Symbol.Name.Substring(3, 3)))
                    .ToList();
            }

            foreach (var symbolname in list)
            {
                if (Positions.Find(tradeInfo.Label, symbolname, tradeInfo.TradeType) != null)
                {
                    return;
                }
            }

            //Calculate trade amount based on ATR
            double atrSize = Math.Round(tradeInfo.Atr.Result.Last(tradeInfo.BarToCheck) / tradeInfo.Symbol.PipSize, 0);
            double tradeAmount = Account.Equity * tradeInfo.RiskPercentage / (tradeInfo.StopLossFactor * atrSize * tradeInfo.Symbol.PipValue);
            tradeAmount = tradeInfo.Symbol.NormalizeVolumeInUnits(tradeAmount / 2, RoundingMode.Down);

            ExecuteMarketOrder(tradeInfo.TradeType, tradeInfo.Symbol.Name, tradeAmount, tradeInfo.Label, tradeInfo.StopLossFactor * atrSize, tradeInfo.TakeProfitFactor * atrSize);
            ExecuteMarketOrder(tradeInfo.TradeType, tradeInfo.Symbol.Name, tradeAmount, tradeInfo.Label, tradeInfo.StopLossFactor * atrSize, null);
        }
    }
}
