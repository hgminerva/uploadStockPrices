using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Mail;

namespace UploadStockPrices
{
    class Program
    {
        // External Class Structure
        public class StockPriceData
        {
            public DateTime QuoteDate { get; set; }
            public decimal OpenPrice { get; set; }
            public decimal HighPrice { get; set; }
            public decimal LowPrice { get; set; }
            public decimal ClosePrice { get; set; }
            public decimal Volume { get; set; }
            public decimal Season { get; set; }
            public int SeasonCount { get; set; }
            public decimal ema1 { get; set; }
            public decimal ema2 { get; set; }
            public decimal macd { get; set; }
            public decimal macdSignal { get; set; }
            public decimal ma1 { get; set; }
            public decimal ma2 { get; set; }
            public decimal ma3 { get; set; }
            public decimal ma4 { get; set; }
            public decimal ma5 { get; set; }
            public decimal ema8 { get; set; }
            public decimal ema21 { get; set; }
            public decimal macdLinear { get; set; }
        }
        public class WinLossData
        {
            public string WinLoss { get; set; }
            public decimal WinLossAverage { get; set; }
        }

        // Update Screener
        private static double getCorrelation(List<StockPriceData> PlotData, int startIndex, int endIndex)
        {
            double X = 0;
            double Y = 0;
            double sumOfX = 0;
            double sumOfY = 0;
            double sumOfX2 = 0;
            double sumOfY2 = 0;
            double sumOfXY = 0;

            for (int i = startIndex - 1; i < endIndex - 1; i++)
            {
                X = X + 1;
                Y = Convert.ToDouble(PlotData[i].ClosePrice);
                sumOfX = sumOfX + X;
                sumOfY = sumOfY + Y;
                sumOfXY = sumOfXY + (X * Y);
                sumOfX2 = sumOfX2 + (X * X);
                sumOfY2 = sumOfY2 + (Y * Y);
            }
            // NΣXY - (ΣX)(ΣY) / Sqrt([NΣX2 - (ΣX)2][NΣY2 - (ΣY)2])
            // return ((X * sumOfXY) - (sumOfX * sumOfX)) / Math.Sqrt((sumOfX2 - (sumOfX * sumOfX)) * (sumOfY2 - (sumOfY * sumOfY)));
            // return sumOfXY / Math.Sqrt(sumOfX2 * sumOfY2);
            return (sumOfXY - ((sumOfX * sumOfY) / X)) / Math.Sqrt(((sumOfX2 - ((sumOfX * sumOfX) / X)) * (sumOfY2 - ((sumOfY * sumOfY) / X)))); 
        }
        private static double getGrowthDecayRate(List<StockPriceData> PlotData, int startIndex, int numberOfDays)
        {
            double growthDecayRate = 0;
            double mPercentage = 0;

            if (Convert.ToDouble(PlotData[startIndex - 1].ClosePrice) > 0)
            {
                mPercentage = ((Convert.ToDouble(PlotData[125].ClosePrice) - Convert.ToDouble(PlotData[startIndex - 1].ClosePrice)) / Convert.ToDouble(PlotData[startIndex - 1].ClosePrice)) * 100;
            }

            if (numberOfDays > 0)
            {
                growthDecayRate = (mPercentage / numberOfDays) * 252;
            }

            return growthDecayRate;
        }
        private static WinLossData getWinLoss(decimal[,] CloseYearData, int startIndex, int endIndex)
        {
            WinLossData returnData = new WinLossData();
            decimal averageWinLoss = 0;
            decimal averageLoss = 0;
            decimal averageWin = 0;
            int Win = 0;
            int Loss = 0;
            // Current: 125,154
            for (int i = 0; i < 10; i++)
            {
                if ((CloseYearData[i, endIndex] + CloseYearData[i, startIndex]) > 0)
                {
                    if (CloseYearData[i, endIndex] >= CloseYearData[i, startIndex])
                    {
                        if (CloseYearData[i, startIndex] > 0)
                            averageWinLoss = ((CloseYearData[i, endIndex] - CloseYearData[i, startIndex]) / CloseYearData[i, startIndex]);
                    }
                    else
                    {
                        if (CloseYearData[i, endIndex] > 0)
                            averageWinLoss = ((CloseYearData[i, endIndex] - CloseYearData[i, startIndex]) / CloseYearData[i, endIndex]);
                    }

                    if (averageWinLoss > 0)
                    {
                        averageWin = averageWin + averageWinLoss;
                        Win++;
                    }
                    else if (averageWinLoss < 0)
                    {
                        averageLoss = averageLoss + averageWinLoss;
                        Loss++;
                    }
                }
            }
            if (Win + Loss > 0)
            {
                if (Win >= Loss)
                {
                    returnData.WinLoss = Convert.ToString(Win) + "W/" + Convert.ToString(Loss) + "L";
                    returnData.WinLossAverage = 0;
                    if (Win > 0) returnData.WinLossAverage = (averageWin / Win) * 100;
                }
                else
                {
                    returnData.WinLoss = Convert.ToString(Loss) + "L/" + Convert.ToString(Win) + "W";
                    returnData.WinLossAverage = 0;
                    if (Loss > 0) returnData.WinLossAverage = (averageLoss / Loss) * 100;
                }
            }
            else
            {
                returnData.WinLoss = "NA";
                returnData.WinLossAverage = 0;
            }
            return returnData;
        }
        private static void UpdateSymbolScreenerData(string DBType, string Exchange, string UpdateNoOfYears)
        {
            int retryCount = 0;
            if (DBType.ToUpper() == "PROD")
            {
                ProdDBDataContext ProdDB = new ProdDBDataContext();

                var Symbols = from d in ProdDB.ProdMstSymbols
                              where d.Exchange == Exchange // && d.Symbol == "MSFT" // Testing Purposes!
                              select d;

                //var GroupSymbols = (from d in ProdDB.ProdTrnStockPrices
                //                   where d.ProdMstSymbol.Exchange == Exchange
                //                   group d by new
                //                   {
                //                        d.Symbol
                //                   } into g
                //                   select new
                //                   {
                //                       Symbol = g.Key.Symbol,
                //                       FirstQuoteDate = g.Min(z => z.QuoteDate)
                //                   }).ToList();

                foreach (ProdMstSymbol Symbol in Symbols)
                {
                    while (true)
                    {
                        try
                        {
                            // Symbol Prices
                            DateTime date2 = Symbol.LatestQuoteDate.Value;
                            DateTime date1 = ((date2.AddDays(1)).AddYears(-10)).AddMonths(-5);
                            var StockPrices = (from d in ProdDB.ProdTrnStockPrices
                                               where (d.Symbol == Symbol.Symbol) &&
                                                     (d.QuoteDate >= date1 && d.QuoteDate <= date2) &&
                                                     (d.ClosePrice > 0) &&
                                                     (Exchange == "FOREX" ? true : (d.Volume > 0))
                                               orderby d.QuoteDate descending
                                               select new StockPriceData
                                               {
                                                   QuoteDate = d.QuoteDate,
                                                   OpenPrice = d.OpenPrice,
                                                   HighPrice = d.HighPrice,
                                                   LowPrice = d.LowPrice,
                                                   ClosePrice = d.ClosePrice,
                                                   Volume = d.Volume,
                                                   Season = 0,
                                                   SeasonCount = 0,
                                                   ema1 = 0,
                                                   ema2 = 0,
                                                   macd = 0,
                                                   macdSignal = 0,
                                                   ma1 = 0,
                                                   ma2 = 0,
                                                   ma3 = 0,
                                                   ma4 = 0,
                                                   ma5 = 0,
                                                   ema8 = 0,
                                                   ema21 = 0
                                               }).Take(2624).ToList();

                            // MACD Variables
                            int macd_index = 0;

                            decimal ema1_closePrices = 0;
                            decimal ema1_previous = 0;
                            decimal ema1 = 0;

                            decimal ema2_closePrices = 0;
                            decimal ema2_previous = 0;
                            decimal ema2 = 0;

                            decimal macd_values = 0;
                            decimal macd_sginal = 0;
                            decimal macd_sginal_previous = 0;

                            // EMA 8-21 Variables
                            decimal ema8 = 0;
                            decimal ema8_closePrices = 0;
                            decimal ema8_previous = 0;
                            decimal ema21 = 0;
                            decimal ema21_closePrices = 0;
                            decimal ema21_previous = 0;

                            for (var i = StockPrices.Count - 1; i >= 0; i--)
                            {
                                // EMA 
                                if (macd_index < 8)
                                {
                                    ema8_closePrices = ema8_closePrices + StockPrices[i].ClosePrice;
                                }
                                else if (macd_index == 11)
                                {
                                    ema8 = ema8_closePrices / 8;
                                    ema8_previous = ema8;
                                    StockPrices[i].ema8 = ema8;
                                }
                                else if (macd_index > 8)
                                {
                                    ema8 = (StockPrices[i].ClosePrice * ((decimal)2 / ((decimal)8 + (decimal)1)) + ema8_previous * ((decimal)1 - ((decimal)2 / ((decimal)8 + (decimal)1))));
                                    ema8_previous = ema8;
                                    StockPrices[i].ema8 = ema8;
                                }

                                if (macd_index < 21)
                                {
                                    ema21_closePrices = ema21_closePrices + StockPrices[i].ClosePrice;
                                }
                                else if (macd_index == 21)
                                {
                                    ema21 = ema21_closePrices / 8;
                                    ema21_previous = ema21;
                                    StockPrices[i].ema21 = ema21;
                                }
                                else if (macd_index > 8)
                                {
                                    ema21 = (StockPrices[i].ClosePrice * ((decimal)2 / ((decimal)21 + (decimal)1)) + ema21_previous * ((decimal)1 - ((decimal)2 / ((decimal)21 + (decimal)1))));
                                    ema21_previous = ema21;
                                    StockPrices[i].ema21 = ema21;
                                }

                                // MACD 
                                if (macd_index < 11)
                                {
                                    ema1_closePrices = ema1_closePrices + StockPrices[i].ClosePrice;
                                }
                                else if (macd_index == 11)
                                {
                                    ema1 = ema1_closePrices / 12;
                                    ema1_previous = ema1;
                                    StockPrices[i].ema1 = ema1;
                                }
                                else if (macd_index > 11)
                                {
                                    ema1 = (StockPrices[i].ClosePrice * ((decimal)2 / ((decimal)12 + (decimal)1)) + ema1_previous * ((decimal)1 - ((decimal)2 / ((decimal)12 + (decimal)1))));
                                    ema1_previous = ema1;
                                    StockPrices[i].ema1 = ema1;
                                }

                                if (macd_index < 25)
                                {
                                    ema2_closePrices = ema2_closePrices + StockPrices[i].ClosePrice;
                                }
                                else if (macd_index == 25)
                                {
                                    ema2 = ema2_closePrices / 26;
                                    ema2_previous = ema2;
                                    StockPrices[i].ema2 = ema2;
                                    StockPrices[i].macd = StockPrices[i].ema1 - ema2;
                                }
                                else if (macd_index > 25)
                                {
                                    ema2 = (StockPrices[i].ClosePrice * ((decimal)2 / ((decimal)26 + (decimal)1)) + ema2_previous * ((decimal)1 - ((decimal)2 / ((decimal)26 + (decimal)1))));
                                    ema2_previous = ema2;
                                    StockPrices[i].ema2 = ema2;
                                    StockPrices[i].macd = StockPrices[i].ema1 - ema2;
                                }

                                if (macd_index < 34)
                                {
                                    macd_values = macd_values + StockPrices[i].macd;
                                }
                                else if (macd_index == 34)
                                {
                                    macd_sginal = macd_values / 9;
                                    macd_sginal_previous = macd_sginal;
                                    StockPrices[i].macdSignal = macd_sginal;
                                }
                                else if (macd_index > 34)
                                {
                                    macd_sginal = (StockPrices[i].macd * ((decimal)2 / ((decimal)9 + (decimal)1)) + macd_sginal * ((decimal)1 - ((decimal)2 / ((decimal)9 + (decimal)1))));
                                    macd_sginal_previous = macd_sginal;
                                    StockPrices[i].macdSignal = macd_sginal;
                                }

                                macd_index++;
                            }

                            // Yearly Closing prices

                            var dayIndex = 126;
                            var countYear = 0;
                            int c = 0;
                            decimal[] FirstClosingPrice = new decimal[10];
                            foreach (StockPriceData data in StockPrices)
                            {
                                data.SeasonCount = dayIndex;
                                dayIndex--;

                                if (dayIndex == 0)
                                {
                                    dayIndex = 252;

                                    if (countYear > 0)
                                    {
                                        FirstClosingPrice[countYear - 1] = data.ClosePrice;
                                    }
                                    countYear++;
                                }
                                else
                                {
                                    if (c == StockPrices.Count() - 1)
                                    {
                                        if (countYear > 0 && countYear < 11)
                                        {
                                            FirstClosingPrice[countYear - 1] = data.ClosePrice;
                                        }
                                    }
                                }
                                c++;
                            }

                            // Yearly Season values
                            decimal season = 0;
                            decimal[,] SeasonYearData = new decimal[10, 252];
                            decimal[,] CloseYearData = new decimal[10, 252];
                            countYear = 0;
                            dayIndex = 251;
                            c = 0;
                            foreach (StockPriceData data in StockPrices)
                            {
                                if (c > 125)
                                {
                                    if (FirstClosingPrice[countYear] > 0)
                                    {
                                        season = ((data.ClosePrice - FirstClosingPrice[countYear]) / FirstClosingPrice[countYear]) * 100;
                                    }
                                    else
                                    {
                                        season = 0;
                                    }

                                    data.Season = season;

                                    SeasonYearData[countYear, dayIndex] = season;
                                    CloseYearData[countYear, dayIndex] = data.ClosePrice;

                                    dayIndex--;
                                    if (dayIndex == -1)
                                    {
                                        dayIndex = 251;
                                        countYear++;
                                        if (countYear > 9) break;
                                    }
                                }
                                c++;
                            }

                            // Average Season values
                            decimal[] SeasonTenYearData = new decimal[252];
                            for (int i = 0; i < 252; i++)
                            {
                                season = 0;
                                for (int y = 0; y < 10; y++)
                                {
                                    season = season + SeasonYearData[y, i];
                                }
                                SeasonTenYearData[i] = season;
                            }

                            // Plotted symbol prices
                            DateTime FutureDate = StockPrices.First().QuoteDate.AddDays(1);
                            List<StockPriceData> PlotData = new List<StockPriceData>();
                            for (int i = 0; i < 252; i++)
                            {
                                if (i > 125)
                                {
                                    if (FutureDate.ToString("ddd") == "Sun" || FutureDate.ToString("ddd") == "Sat")
                                    {
                                        i--;
                                    }
                                    else
                                    {
                                        PlotData.Add(new StockPriceData
                                        {
                                            QuoteDate = FutureDate,
                                            Season = SeasonTenYearData[i] / 10,
                                            SeasonCount = i + 1
                                        });
                                    }
                                    FutureDate = FutureDate.AddDays(1);
                                }
                                else
                                {
                                    StockPriceData data = StockPrices[125 - i];
                                    PlotData.Add(new StockPriceData
                                    {
                                        QuoteDate = data.QuoteDate,
                                        OpenPrice = data.OpenPrice,
                                        HighPrice = data.HighPrice,
                                        LowPrice = data.LowPrice,
                                        ClosePrice = data.ClosePrice,
                                        Volume = data.Volume,
                                        Season = SeasonTenYearData[i] / 10,
                                        SeasonCount = i + 1,
                                        macd = data.macd,
                                        macdSignal = data.macdSignal,
                                        ema8 = data.ema8,
                                        ema21 = data.ema21
                                    });
                                }
                            }

                            // =================
                            // Get the EMA trend
                            // =================
                            int EMADayIndex = 0;
                            decimal EMAPrice = 0;
                            int EMATrendNoOfDays = 0;
                            decimal EMAGrowthDecayRate = 0;
                            bool EMAAhead = false;
                            DateTime EMAStartDate = new DateTime();
                            int EMALastCrossoverNoOfDays = 0;
                            bool EMALastCrossoverMonitor = false;

                            for (int e = 0; e < 126; e++)
                            {
                                if (e > 0)
                                {
                                    if (PlotData[e].ema21 < 0 && PlotData[e].ema8 < 0)
                                    {
                                        if (EMAAhead == false && PlotData[e].ema21 > PlotData[e].ema8)
                                        {
                                            EMADayIndex = e;
                                            EMAPrice = PlotData[e].ClosePrice;
                                            EMAStartDate = PlotData[e].QuoteDate;
                                            EMAAhead = true;
                                        }
                                        else if (EMAAhead == true && PlotData[e].ema21 <= PlotData[e].ema8)
                                        {
                                            EMADayIndex = e;
                                            EMAPrice = PlotData[e].ClosePrice;
                                            EMAStartDate = PlotData[e].QuoteDate;
                                            EMAAhead = false;
                                        }
                                    }
                                    else
                                    {
                                        if (EMAAhead == true && PlotData[e].ema21 <= PlotData[e].ema8)
                                        {
                                            EMADayIndex = e;
                                            EMAPrice = PlotData[e].ClosePrice;
                                            EMAStartDate = PlotData[e].QuoteDate;
                                            EMAAhead = false;
                                        }
                                        else if (EMAAhead == false && PlotData[e].ema21 > PlotData[e].ema8)
                                        {
                                            EMADayIndex = e;
                                            EMAPrice = PlotData[e].ClosePrice;
                                            EMAStartDate = PlotData[e].QuoteDate;
                                            EMAAhead = true;
                                        }
                                    }
                                    if (EMALastCrossoverMonitor != EMAAhead)
                                    {
                                        EMALastCrossoverNoOfDays = 1;
                                        EMALastCrossoverMonitor = EMAAhead;
                                    }
                                    else
                                    {
                                        EMALastCrossoverNoOfDays++;
                                    }
                                }
                                else
                                {
                                    if (PlotData[e].ema21 < 0 && PlotData[e].ema8 < 0)
                                    {
                                        if (PlotData[e].ema21 <= PlotData[e].ema8)
                                        {
                                            EMAAhead = true;
                                        }
                                        else
                                        {
                                            EMAAhead = false;
                                        }
                                    }
                                    else
                                    {
                                        if (PlotData[e].ema21 > PlotData[e].ema8)
                                        {
                                            EMAAhead = true;
                                        }
                                        else
                                        {
                                            EMAAhead = false;
                                        }
                                    }
                                    EMALastCrossoverMonitor = EMAAhead;
                                }
                            }
                            if (EMADayIndex < 125)
                            {
                                EMATrendNoOfDays = 125 - EMADayIndex;
                                if (PlotData[125].ClosePrice > EMAPrice)
                                {
                                    EMAGrowthDecayRate = ((PlotData[125].ClosePrice - EMAPrice) / EMAPrice) * 100;
                                }
                                else
                                {
                                    EMAGrowthDecayRate = ((PlotData[125].ClosePrice - EMAPrice) / PlotData[125].ClosePrice) * 100;
                                }
                            }

                            // ==================
                            // Get the MACD trend
                            // ==================
                            int MACDDayIndex = 0;
                            decimal MACDPrice = 0;
                            int MACDTrendNoOfDays = 0;
                            decimal MACDGrowthDecayRate = 0;
                            bool MACDAhead = false;

                            int MACDLastCrossoverNoOfDays = 0;
                            bool MACDLastCrossoverMonitor = false;
                            string MACDPosition = "ALL";

                            for (int m = 0; m < 126; m++)
                            {
                                if (m > 0)
                                {
                                    if (PlotData[m].macd < 0 && PlotData[m].macdSignal < 0)
                                    {
                                        if (MACDAhead == false && PlotData[m].macd > PlotData[m].macdSignal)
                                        {
                                            MACDDayIndex = m;
                                            MACDPrice = PlotData[m].ClosePrice;
                                            MACDAhead = true;
                                        }
                                        else if (MACDAhead == true && PlotData[m].macd <= PlotData[m].macdSignal)
                                        {
                                            MACDDayIndex = m;
                                            MACDPrice = PlotData[m].ClosePrice;
                                            MACDAhead = false;
                                        }
                                    }
                                    else
                                    {
                                        if (MACDAhead == true && PlotData[m].macd <= PlotData[m].macdSignal)
                                        {
                                            MACDDayIndex = m;
                                            MACDPrice = PlotData[m].ClosePrice;
                                            MACDAhead = false;
                                        }
                                        else if (MACDAhead == false && PlotData[m].macd > PlotData[m].macdSignal)
                                        {
                                            MACDDayIndex = m;
                                            MACDPrice = PlotData[m].ClosePrice;
                                            MACDAhead = true;
                                        }
                                    }
                                    if (MACDLastCrossoverMonitor != MACDAhead)
                                    {
                                        MACDLastCrossoverNoOfDays = 1;
                                        if (MACDAhead == true) MACDPosition = "UP";
                                        else MACDPosition = "DOWN";
                                        MACDLastCrossoverMonitor = MACDAhead;
                                    }
                                    else
                                    {
                                        MACDLastCrossoverNoOfDays++;
                                    }
                                }
                                else
                                {
                                    if (PlotData[m].macd < 0 && PlotData[m].macdSignal < 0)
                                    {
                                        if (PlotData[m].macd <= PlotData[m].macdSignal)
                                        {
                                            MACDAhead = true;
                                        }
                                        else
                                        {
                                            MACDAhead = false;
                                        }
                                    }
                                    else
                                    {
                                        if (PlotData[m].macd > PlotData[m].macdSignal)
                                        {
                                            MACDAhead = true;
                                        }
                                        else
                                        {
                                            MACDAhead = false;
                                        }
                                    }
                                    MACDLastCrossoverMonitor = MACDAhead;
                                }
                            }
                            if (MACDDayIndex < 125)
                            {
                                MACDTrendNoOfDays = 125 - MACDDayIndex;
                                if (PlotData[125].ClosePrice > MACDPrice)
                                {
                                    MACDGrowthDecayRate = ((PlotData[125].ClosePrice - MACDPrice) / MACDPrice) * 100;
                                }
                                else
                                {
                                    MACDGrowthDecayRate = ((PlotData[125].ClosePrice - MACDPrice) / PlotData[125].ClosePrice) * 100;
                                }
                            }

                            // =================
                            // Get current trend
                            // =================
                            int counter = 0;
                            double coefficient = 0;
                            double coefficient30 = 0;
                            int numberOfDays = 0;
                            int start = 126;
                            for (int g = 126; g > 0; g--)
                            {
                                if (counter > 30)
                                {
                                    coefficient = Math.Abs(getCorrelation(PlotData, g, 126));
                                    if (coefficient <= 0.9)
                                    {
                                        start = g;
                                        numberOfDays = 126 - g + 1;
                                        break;
                                    }
                                }
                                else if (counter == 30)
                                {
                                    coefficient30 = Math.Abs(getCorrelation(PlotData, g, 126));
                                }
                                counter++;
                            }

                            double growthDecayRate = 0;
                            double growthDecayRateW1 = 0;
                            double growthDecayRateW2 = 0;
                            double growthDecayRateW3 = 0;
                            double growthDecayRateM1 = 0;
                            double growthDecayRateM2 = 0;
                            double growthDecayRateM3 = 0;

                            growthDecayRate = getGrowthDecayRate(PlotData, start, numberOfDays);
                            growthDecayRateW1 = getGrowthDecayRate(PlotData, 121, 5);
                            growthDecayRateW2 = getGrowthDecayRate(PlotData, 116, 10);
                            growthDecayRateW3 = getGrowthDecayRate(PlotData, 111, 15);
                            growthDecayRateM1 = getGrowthDecayRate(PlotData, 106, 20);
                            growthDecayRateM2 = getGrowthDecayRate(PlotData, 86, 40);
                            growthDecayRateM3 = getGrowthDecayRate(PlotData, 66, 60);

                            Symbol.ClosePrice = PlotData[125].ClosePrice;
                            Symbol.Volume = PlotData[125].Volume;
                            Symbol.GrowthDecayRate = Convert.ToDecimal(growthDecayRate);
                            Symbol.GrowthDecayRateW1 = Convert.ToDecimal(growthDecayRateW1);
                            Symbol.GrowthDecayRateW2 = Convert.ToDecimal(growthDecayRateW2);
                            Symbol.GrowthDecayRateW3 = Convert.ToDecimal(growthDecayRateW3);
                            Symbol.GrowthDecayRateM1 = Convert.ToDecimal(growthDecayRateM1);
                            Symbol.GrowthDecayRateM2 = Convert.ToDecimal(growthDecayRateM2);
                            Symbol.GrowthDecayRateM3 = Convert.ToDecimal(growthDecayRateM3);
                            Symbol.TrendNoOfDays = counter;

                            // MACD
                            Symbol.MACDTrendNoOfDays = MACDTrendNoOfDays;

                            if (MACDTrendNoOfDays > 0) MACDGrowthDecayRate = (MACDGrowthDecayRate / MACDTrendNoOfDays) * 252;
                            else MACDGrowthDecayRate = 0;
                            Symbol.MACDGrowthDecayRate = MACDGrowthDecayRate;

                            // EMA
                            Symbol.EMATrendNoOfDays = EMATrendNoOfDays;

                            if (EMATrendNoOfDays > 0) EMAGrowthDecayRate = (EMAGrowthDecayRate / EMATrendNoOfDays) * 252;
                            else EMAGrowthDecayRate = 0;
                            Symbol.EMAGrowthDecayRate = EMAGrowthDecayRate;

                            Symbol.EMAStartDate = EMAStartDate.Date;

                            double EMALinear = Math.Abs(getCorrelation(PlotData, EMADayIndex + 1, 126));
                            Symbol.EMALinear = Convert.ToDecimal(EMALinear);

                            // Win/Loss
                            WinLossData WinLossCurrent = getWinLoss(CloseYearData, 125, 154);
                            WinLossData WinLoss20 = getWinLoss(CloseYearData, 125, 144);
                            WinLossData WinLoss40 = getWinLoss(CloseYearData, 125, 164);
                            WinLossData WinLoss60 = getWinLoss(CloseYearData, 125, 184);

                            Symbol.WinLossCurrent30 = WinLossCurrent.WinLoss;
                            Symbol.WinLossAverageCurrent30 = WinLossCurrent.WinLossAverage;
                            Symbol.WinLoss20 = WinLoss20.WinLoss;
                            Symbol.WinLossAverage20 = WinLoss20.WinLossAverage;
                            Symbol.WinLoss40 = WinLoss40.WinLoss;
                            Symbol.WinLossAverage40 = WinLoss40.WinLossAverage;
                            Symbol.WinLoss60 = WinLoss60.WinLoss;
                            Symbol.WinLossAverage60 = WinLoss60.WinLossAverage;

                            Symbol.CorrelationCoefficient30 = Convert.ToDecimal(coefficient30);

                            if (UpdateNoOfYears.ToUpper() == "Y")
                            {
                                DateTime FirstQuoteDate = StockPrices.Min(d => d.QuoteDate);
                                //DateTime FirstQuoteDate = GroupSymbols.Where(p => p.Symbol == Symbol.Symbol).FirstOrDefault().FirstQuoteDate;
                                int NoOfYears = Symbol.LatestQuoteDate.Value.Year - FirstQuoteDate.Year;
                                Symbol.NoOfYears = NoOfYears;
                            }

                            double[] corrPriceData = new double[126];
                            double[] corrSeasonData = new double[126];
                            for (int s = 0; s < 126; s++)
                            {
                                corrPriceData[s] = (double)PlotData[s].ClosePrice;
                                corrSeasonData[s] = (double)PlotData[s].Season;
                            }
                            SampleStatistics ss = new SampleStatistics();
                            Symbol.SeasonalityCorrelation = Math.Abs((decimal)ss.sample_correlation(corrPriceData, corrSeasonData));

                            Symbol.MACDLastCrossoverNoOfDays = MACDLastCrossoverNoOfDays;
                            Symbol.MACDPosition = MACDPosition;
                            Symbol.EMALastCrossoverNoOfDays = EMALastCrossoverNoOfDays;

                            // Specific date, e.g., Nov 7 2016 (US Election)
                            // DateTime Nov72016 = DateTime.Parse("11/7/2016");
                            // DateTime Jan22018 = DateTime.Parse("1/2/2018");
                            DateTime Oct122018 = DateTime.Parse("10/12/2018");
                            counter = 0;
                            for (int n = 126; n > 0; n--)
                            {
                                if (PlotData[n].QuoteDate == Oct122018)
                                {
                                    Symbol.Nov7ClosePrice = PlotData[n].ClosePrice;
                                    Symbol.Nov7NumberOfDays = counter;
                                    Symbol.Nov7CorrelationCoefficient = Math.Abs((decimal)getCorrelation(PlotData,126-counter, 126));
                                }
                                counter++;
                            }

                            // MACD Linear
                            double MACDLinear = Math.Abs(getCorrelation(PlotData, MACDDayIndex + 1, 126));
                            Symbol.MACDLinear = Convert.ToDecimal(MACDLinear);

                            ProdDB.SubmitChanges();

                            Console.WriteLine("Update screener data of " + Symbol.Symbol);
                            break;
                        }
                        catch
                        {
                            retryCount++;
                            if (retryCount == 2)
                            {
                                retryCount = 0;
                                break;
                            }
                            Console.WriteLine("Link failed.  Retrying...");
                            Thread.Sleep(1000);
                        }
                    }
                }
            }
        }

        // Update Latest Quote Date
        private static void UpdateLatestQuoteDate(string DBType, string Exchange)
        {
            if (DBType.ToUpper() == "LOCAL")
            {

            }
            else if (DBType.ToUpper() == "PROD")
            {
                ProdDBDataContext ProdDB = new ProdDBDataContext();
                var Symbols = from d in ProdDB.ProdMstSymbols where d.Exchange == Exchange select d;
                foreach (ProdMstSymbol Symbol in Symbols)
                {
                    while (true)
                    {
                        try
                        {
                            if (ProdDB.ProdTrnStockPrices.Where(d => d.Symbol == Symbol.Symbol).Count() > 0)
                            {
                                Symbol.LatestQuoteDate = ProdDB.ProdTrnStockPrices.Where(d => d.Symbol == Symbol.Symbol).Max(s => s.QuoteDate);
                                ProdDB.SubmitChanges();

                                Console.WriteLine("Update Latest Quote Date of " + Symbol.Symbol);
                                break;
                            }
                        }
                        catch
                        {
                            Console.WriteLine("Link failed.  Retrying...");
                            Thread.Sleep(1000);
                        }
                    }
                }
            }
            else
            {

            }  
        }
        
        // Send Alert
        private static List<ProdMstSymbol> GetResultLevelSymbol(long userAlertId)
        {
            ProdDBDataContext dbProd = new ProdDBDataContext();
            List<ProdMstSymbol> symbols = new List<ProdMstSymbol>();
            string exchange;

            if (userAlertId > 0)
            {
                var userAlerts = from d in dbProd.ProdTrnUserAlerts where d.Id == userAlertId select d;
                exchange = userAlerts.First().SymbolExchange;
                if (userAlerts.Any())
                {
                    var filterSymbols = from d in dbProd.ProdMstSymbols select d;
                    if (exchange == "US")
                    {
                        filterSymbols = from d in dbProd.ProdMstSymbols
                                        where d.Exchange == "AMEX" || d.Exchange == "NYSE" || d.Exchange == "NASDAQ"
                                        select d;
                    }
                    else
                    {
                        filterSymbols = from d in dbProd.ProdMstSymbols
                                        where d.Exchange == exchange
                                        select d;
                    }
                    foreach (ProdMstSymbol s in filterSymbols)
                    {
                        symbols.Add(s);
                    }
                }
            }
            return symbols.ToList();
        }
        private static List<ProdMstSymbol> GetResultLevelStrategy(long userAlertId, List<ProdMstSymbol> filteredSymbols)
        {
            ProdDBDataContext dbProd = new ProdDBDataContext();
            List<ProdMstSymbol> symbols = new List<ProdMstSymbol>();
            string strategy;
            if (filteredSymbols.Any())
            {
                if (userAlertId > 0)
                {
                    var userAlerts = from d in dbProd.ProdTrnUserAlerts where d.Id == userAlertId select d;
                    if (userAlerts.Any())
                    {
                        strategy = userAlerts.First().Strategy;
                        var strategySymbols = from d in filteredSymbols select d;
                        if (strategy == "MED")
                        {
                            strategySymbols = from d in filteredSymbols
                                              where d.MACDGrowthDecayRate < 0 && d.EMAGrowthDecayRate < 0
                                              select d;
                        }
                        else if (strategy == "MEU")
                        {
                            strategySymbols = from d in filteredSymbols
                                              where d.MACDGrowthDecayRate >= 0 && d.EMAGrowthDecayRate >= 0
                                              select d;
                        }
                        foreach (ProdMstSymbol s in strategySymbols)
                        {
                            symbols.Add(s);
                        }
                    }
                }
            }
            return symbols.ToList();
        }
        private static List<ProdMstSymbol> GetResultLevelMACD(long userAlertId, List<ProdMstSymbol> filteredSymbols)
        {
            ProdDBDataContext dbProd = new ProdDBDataContext();
            List<ProdMstSymbol> symbols = new List<ProdMstSymbol>();
            string macdCrossover;
            string macdEMA;
            if (filteredSymbols.Any())
            {
                if (userAlertId > 0)
                {
                    var userAlerts = from d in dbProd.ProdTrnUserAlerts where d.Id == userAlertId select d;
                    if (userAlerts.Any())
                    {
                        macdCrossover = userAlerts.First().MACDCrossover;
                        macdEMA = userAlerts.First().MACDEMA;
                        var macdSymbols = from d in filteredSymbols select d;
                        if (macdEMA == "BEFORE")
                        {
                            macdSymbols = from d in filteredSymbols
                                          where macdCrossover == "ALL" ? true : d.MACDPosition == macdCrossover &&
                                                d.MACDLastCrossoverNoOfDays > d.EMALastCrossoverNoOfDays
                                          select d;
                        }
                        else if (macdEMA == "AFTER")
                        {
                            macdSymbols = from d in filteredSymbols
                                          where macdCrossover == "ALL" ? true : d.MACDPosition == macdCrossover &&
                                                d.MACDLastCrossoverNoOfDays <= d.EMALastCrossoverNoOfDays
                                          select d;
                        }
                        else
                        {
                            macdSymbols = from d in filteredSymbols
                                          where macdCrossover == "ALL" ? true : d.MACDPosition == macdCrossover
                                          select d;
                        }
                        foreach (ProdMstSymbol s in macdSymbols)
                        {
                            symbols.Add(s);
                        }
                    }
                }
            }
            return symbols.ToList();
        }
        private static List<ProdMstSymbol> GetResultLevelMagentaChannel(long userAlertId, List<ProdMstSymbol> filteredSymbols)
        {
            ProdDBDataContext dbProd = new ProdDBDataContext();
            List<ProdMstSymbol> symbols = new List<ProdMstSymbol>();
            string magentaChannelBegins;
            int magentaChannelCorrelation30;
            int magentaChannelDays;
            decimal magentaChannelAGRADR;
            if (filteredSymbols.Any())
            {
                if (userAlertId > 0)
                {
                    var userAlerts = from d in dbProd.ProdTrnUserAlerts where d.Id == userAlertId select d;
                    if (userAlerts.Any())
                    {
                        magentaChannelBegins = userAlerts.First().MagentaChannelBegins;
                        magentaChannelCorrelation30 = userAlerts.First().MagentaChannelCorrelation30;
                        magentaChannelDays = userAlerts.First().MagentaChannelDays;
                        magentaChannelAGRADR = userAlerts.First().MagentaChannelAGRADR;

                        var magentaChannelSymbols = from d in filteredSymbols select d;

                        if (magentaChannelBegins == "ALL")
                        {
                            magentaChannelSymbols = from d in filteredSymbols
                                                    where d.CorrelationCoefficient30 >= magentaChannelCorrelation30 &&
                                                          d.TrendNoOfDays >= magentaChannelDays &&
                                                          magentaChannelAGRADR >= 0 ? d.GrowthDecayRate >= magentaChannelAGRADR : d.GrowthDecayRate <= magentaChannelAGRADR
                                                    select d;
                        }
                        else if (magentaChannelBegins == "MACD")
                        {
                            magentaChannelSymbols = from d in filteredSymbols
                                                    where d.TrendNoOfDays >= d.MACDLastCrossoverNoOfDays &&
                                                          d.CorrelationCoefficient30 >= magentaChannelCorrelation30 &&
                                                          d.TrendNoOfDays >= d.MACDLastCrossoverNoOfDays &&
                                                          magentaChannelAGRADR >= 0 ? d.GrowthDecayRate >= magentaChannelAGRADR : d.GrowthDecayRate <= magentaChannelAGRADR
                                                    select d;
                        }
                        else if (magentaChannelBegins == "EMA")
                        {
                            magentaChannelSymbols = from d in filteredSymbols
                                                    where d.TrendNoOfDays >= d.EMALastCrossoverNoOfDays &&
                                                          d.CorrelationCoefficient30 >= magentaChannelCorrelation30 &&
                                                          d.TrendNoOfDays >= d.EMALastCrossoverNoOfDays &&
                                                          magentaChannelAGRADR >= 0 ? d.GrowthDecayRate >= magentaChannelAGRADR : d.GrowthDecayRate <= magentaChannelAGRADR
                                                    select d;
                        }
                        foreach (ProdMstSymbol s in magentaChannelSymbols)
                        {
                            symbols.Add(s);
                        }
                    }
                }
            }
            return symbols.ToList();
        }
        private static List<ProdMstSymbol> GetResultLevelSeasonality(long userAlertId, List<ProdMstSymbol> filteredSymbols)
        {
            ProdDBDataContext dbProd = new ProdDBDataContext();
            List<ProdMstSymbol> symbols = new List<ProdMstSymbol>();
            decimal seasonalityWinLossPercent;
            decimal seasonalityGainLossPercent;
            if (filteredSymbols.Any())
            {
                if (userAlertId > 0)
                {
                    var userAlerts = from d in dbProd.ProdTrnUserAlerts where d.Id == userAlertId select d;
                    if (userAlerts.Any())
                    {
                        seasonalityWinLossPercent = userAlerts.First().SeasonalityWinLossPercent;
                        seasonalityGainLossPercent = userAlerts.First().SeasonalityGainLossPercent;

                        var seasonalitySymbols = from d in filteredSymbols
                                                 where d.WinLossAverageCurrent30 >= seasonalityGainLossPercent
                                                 select d;

                        foreach (ProdMstSymbol s in seasonalitySymbols)
                        {
                            symbols.Add(s);
                        }
                    }
                }
            }
            return symbols.ToList();
        }
        private static List<ProdMstSymbol> GetResultLevelAdditionalFilter(long userAlertId, List<ProdMstSymbol> filteredSymbols)
        {
            ProdDBDataContext dbProd = new ProdDBDataContext();
            List<ProdMstSymbol> symbols = new List<ProdMstSymbol>();
            decimal additionalFilterPrice;
            decimal additionalFilterVolume;
            int additionalFilterNoOfYears;
            if (filteredSymbols.Any())
            {
                if (userAlertId > 0)
                {
                    var userAlerts = from d in dbProd.ProdTrnUserAlerts where d.Id == userAlertId select d;
                    if (userAlerts.Any())
                    {
                        additionalFilterPrice = userAlerts.First().AdditionalFilterPrice;
                        additionalFilterVolume = userAlerts.First().AdditionalFilterVolume;
                        additionalFilterNoOfYears = userAlerts.First().AdditionalFilterNoOfYears;

                        var additionalFilterSymbols = from d in filteredSymbols
                                                      where d.ClosePrice >= additionalFilterPrice &&
                                                            d.Volume >= additionalFilterVolume &&
                                                            d.NoOfYears >= additionalFilterNoOfYears
                                                      select d;

                        foreach (ProdMstSymbol s in additionalFilterSymbols)
                        {
                            symbols.Add(s);
                        }
                    }
                }
            }
            return symbols.ToList();
        }
        private static void SendAlert()
        {
            ProdDBDataContext dbProd = new ProdDBDataContext();

            long userAlertId;
            bool symbolFilter;
            bool strategyFilter;
            bool macdFilter;
            bool magentaChannelFilter;
            bool seasonalityFilter;
            bool additionalFilter;

            var userAlerts = from d in dbProd.ProdTrnUserAlerts where d.IsActive == true select d;
            if (userAlerts.Any())
            {
                foreach (var userAlert in userAlerts)
                {
                    List<ProdMstSymbol> symbolResults = new List<ProdMstSymbol>();
                    userAlertId = userAlert.Id;
                    symbolFilter = userAlert.SymbolFilter;
                    strategyFilter = userAlert.StrategyFilter;
                    macdFilter = userAlert.MACDFilter;
                    magentaChannelFilter = userAlert.MagentaChannelFilter;
                    seasonalityFilter = userAlert.SeasonalityFilter;
                    additionalFilter = userAlert.AdditionalFilter;

                    if (symbolFilter == true)
                    {
                        symbolResults = GetResultLevelSymbol(userAlertId);
                        if (strategyFilter == true)
                        {
                            symbolResults = GetResultLevelStrategy(userAlertId, symbolResults);
                        }
                        if (macdFilter == true)
                        {
                            symbolResults = GetResultLevelMACD(userAlertId, symbolResults);
                        }
                        if (magentaChannelFilter == true)
                        {
                            symbolResults = GetResultLevelMagentaChannel(userAlertId, symbolResults);
                        }
                        if (seasonalityFilter == true)
                        {
                            symbolResults = GetResultLevelSeasonality(userAlertId, symbolResults);
                        }
                        if (additionalFilter == true)
                        {
                            symbolResults = GetResultLevelAdditionalFilter(userAlertId, symbolResults);
                        }
                    }

                    // Delete existing data
                    var userAlertSymbols = from d in dbProd.ProdTrnUserAlertSymbols where d.UserAlertId == userAlertId select d;
                    if (userAlertSymbols.Any())
                    {
                        foreach (ProdTrnUserAlertSymbol s in userAlertSymbols)
                        {
                            dbProd.ProdTrnUserAlertSymbols.DeleteOnSubmit(s);
                            dbProd.SubmitChanges();
                        }
                    }

                    // Add symbols
                    if (symbolResults.Any())
                    {
                        foreach (var s in symbolResults)
                        {
                            ProdTrnUserAlertSymbol newUserAlertSymbol = new ProdTrnUserAlertSymbol();

                            newUserAlertSymbol.UserAlertId = Convert.ToInt16(userAlertId);
                            newUserAlertSymbol.SymbolId = s.Id;
                            newUserAlertSymbol.Symbol = s.Symbol;
                            newUserAlertSymbol.Trend = "";
                            newUserAlertSymbol.EncodedDate = userAlerts.First().EncodedDate;

                            dbProd.ProdTrnUserAlertSymbols.InsertOnSubmit(newUserAlertSymbol);
                            dbProd.SubmitChanges();
                        }

                        if (userAlert.AlertVia == "Email")
                        {
                            string myHTML = "";
                            MailMessage mail = new MailMessage();
                            SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
                            mail.From = new MailAddress("Magenta.Trader.Alert@gmail.com");
                            mail.To.Add(userAlert.ProdMstUser.EmailAddress);
                            mail.Subject = "Magenta Trader Alert";
                            mail.Body = "Symbols - " + DateTime.Now.ToString("MM/dd/yyyy") + Environment.NewLine + myHTML;

                            //System.Net.Mail.Attachment attachment;
                            //attachment = new System.Net.Mail.Attachment("c:/textfile.txt");
                            //mail.Attachments.Add(attachment);

                            //SmtpServer.Port = 587;
                            //SmtpServer.Credentials = new System.Net.NetworkCredential("magenta.trader.alert@gmail.com", "@magenta1");
                            //SmtpServer.EnableSsl = true;

                            //SmtpServer.Send(mail);
                            Console.WriteLine("Magenta Alert Sent - " + userAlert.ProdMstUser.EmailAddress);
                        }
                    }
                }
            }
            Console.WriteLine("Checking ...");
        }

        // Main Program
        static void Main(string[] args)
        {
            string line;
            int counter = 0;
            bool duplicateFlag = false;
            bool checkingFlag = false;
            bool firstRecord = true;
            bool updateScreenerData = false;
            bool sendAlert = false;

            Console.WriteLine("Stock Price Uploader v1.20181019"); 
            Console.Write("DB (Prod/Cloud/Local)? : "); 
            string DBType = Console.ReadLine();
            Console.Write("Exchange? : ");
            string Exchange = Console.ReadLine();
            Console.Write("Re-compute screener data (Y/N)? : ");
            string RecomputeScreener = Console.ReadLine();
            Console.Write("Update No. of Years (Y/N)? : ");
            string UpdateNoOfYears = Console.ReadLine();
            Console.Write("Send Alert (Y/N)? : ");
            string SendAlertNow = Console.ReadLine();

            //UpdateLatestQuoteDate(DBType, Exchange);
            if (DBType.ToUpper() == "PROD" && RecomputeScreener.ToUpper() == "Y")
            {
                UpdateSymbolScreenerData(DBType, Exchange, UpdateNoOfYears);
                Console.WriteLine("Checking " + Exchange + "...");
            }

            if (SendAlertNow.ToUpper() == "Y") SendAlert();

            System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo("C:\\eoddata\\download\\" + Exchange);

            while (true)
            {
                // Loop through the directory of files
                foreach (System.IO.FileInfo file in dir.GetFiles(Exchange + "*.txt"))
                {
                    Console.WriteLine("{0}, {1}", file.Name, file.Length);

                    counter = 0;
                    System.IO.StreamReader choosenFile;

                    // Open the file
                    while (true)
                    {
                        try
                        {
                            choosenFile = new System.IO.StreamReader("C:\\eoddata\\download\\" + Exchange + "\\" + file.Name);
                            break;
                        }
                        catch (IOException)
                        {
                            Console.WriteLine("Error openning file.  Retrying...");
                            Thread.Sleep(1000);
                        }
                    }

                    // Inform the system that it is the first line/record
                    firstRecord = true;

                    // Read the file
                    while ((line = choosenFile.ReadLine()) != null)
                    {
                        // Parse the line of the file
                        // Expected format: AAME,19940103,1.75,1.75,1.75,1.75,4500 - METASTOCK ASCII 7 COLUMNS
                        String[] columns = line.Split(','); 
                        String Symbol = columns[0];
                        DateTime QuoteDate = Convert.ToDateTime(columns[1].Substring(0, 4) + "-" + columns[1].Substring(4, 2) + "-" + columns[1].Substring(6, 2));
                        decimal OpenPrice = Convert.ToDecimal(columns[2]);
                        decimal HighPrice = Convert.ToDecimal(columns[3]);
                        decimal LowPrice = Convert.ToDecimal(columns[4]);
                        decimal ClosePrice = Convert.ToDecimal(columns[5]);
                        decimal Volume = Convert.ToDecimal(columns[6]);

                        // Check if existing.  Check only at the beginning of the file
                        if (firstRecord == false)
                        {
                            duplicateFlag = false;
                        }
                        else
                        {
                            while (true)
                            {
                                try
                                {
                                    if (DBType.ToUpper() == "LOCAL")
                                    {
                                        //LocalDBDataContext dbCheckLocal = new LocalDBDataContext();
                                        //if (dbCheckLocal.LocalTrnStockPrices.Where(d => d.QuoteDate == QuoteDate && d.LocalMstSymbol.Exchange == Exchange).Count() > 0)
                                        //{
                                        //    duplicateFlag = true;
                                        //}
                                        //else
                                        //{
                                        //    duplicateFlag = false;
                                        //}
                                        //break;
                                    }
                                    if (DBType.ToUpper() == "PROD")
                                    {
                                        ProdDBDataContext dbCheckProd = new ProdDBDataContext();
                                        if (dbCheckProd.ProdTrnStockPrices.Where(d => d.QuoteDate == QuoteDate && d.ProdMstSymbol.Exchange == Exchange).Count() > 0)
                                        {
                                            duplicateFlag = true;
                                        }
                                        else
                                        {
                                            duplicateFlag = false;
                                        }
                                        break;
                                    }
                                    else
                                    {
                                        //CloudDBDataContext dbCheck = new CloudDBDataContext();
                                        //if (dbCheck.CloudTrnStockPrices.Where(d => d.QuoteDate == QuoteDate && d.CloudMstSymbol.Exchange == Exchange).Count() > 0)
                                        //{
                                        //    duplicateFlag = true;
                                        //}
                                        //else
                                        //{
                                        //    duplicateFlag = false;
                                        //}
                                        //break;
                                    }
                                }
                                catch
                                {
                                    Console.WriteLine("Link failed.  Retrying...");
                                    Thread.Sleep(1000);
                                }
                            }
                        }

                        // End the reading of the file if we find duplicate records in the database
                        if (duplicateFlag == true)
                        {
                            Console.WriteLine("Duplicate record detected.");
                            choosenFile.Close();
                            File.Delete("C:\\eoddata\\download\\" + Exchange + "\\" + file.Name);
                            checkingFlag = true;
                            break;
                        }

                        // Inform the loop that it is not anymore the first record/line of the file
                        firstRecord = false;

                        // Save to database
                        while (true)
                        {
                            try
                            {
                                if (DBType.ToUpper() == "LOCAL")
                                {
                                    //LocalDBDataContext dbLocal = new LocalDBDataContext();
                                    //LocalTrnStockPrice NewLocalStockPrice = new LocalTrnStockPrice();

                                    //if (dbLocal.LocalMstSymbols.Where(d => d.Symbol == Symbol && d.Exchange == Exchange).Count() > 0)
                                    //{
                                    //    NewLocalStockPrice.SymbolId = dbLocal.LocalMstSymbols.Where(d => d.Symbol == Symbol && d.Exchange == Exchange).FirstOrDefault().Id;
                                    //    NewLocalStockPrice.Symbol = Symbol;
                                    //    NewLocalStockPrice.QuoteDate = QuoteDate;
                                    //    NewLocalStockPrice.OpenPrice = OpenPrice;
                                    //    NewLocalStockPrice.HighPrice = HighPrice;
                                    //    NewLocalStockPrice.LowPrice = LowPrice;
                                    //    NewLocalStockPrice.ClosePrice = ClosePrice;
                                    //    NewLocalStockPrice.Volume = Volume;

                                    //    dbLocal.LocalTrnStockPrices.InsertOnSubmit(NewLocalStockPrice);
                                    //    dbLocal.SubmitChanges();
                                    //}
                                    //break;
                                }
                                if (DBType.ToUpper() == "PROD")
                                {
                                    ProdDBDataContext dbProd = new ProdDBDataContext();
                                    ProdTrnStockPrice NewProdStockPrice = new ProdTrnStockPrice();

                                    if (Exchange == "TSX")
                                    {
                                        Symbol = "TSX-" + Symbol;
                                    }
                                    else if (Exchange == "FOREX")
                                    {
                                        Symbol = "FX-" + Symbol;
                                    }

                                    if (dbProd.ProdMstSymbols.Where(d => d.Symbol == Symbol && d.Exchange == Exchange).Count() > 0)
                                    {
                                        NewProdStockPrice.SymbolId = dbProd.ProdMstSymbols.Where(d => d.Symbol == Symbol && d.Exchange == Exchange).FirstOrDefault().Id;
                                        NewProdStockPrice.Symbol = Symbol;
                                        NewProdStockPrice.QuoteDate = QuoteDate;
                                        NewProdStockPrice.OpenPrice = OpenPrice;
                                        NewProdStockPrice.HighPrice = HighPrice;
                                        NewProdStockPrice.LowPrice = LowPrice;
                                        NewProdStockPrice.ClosePrice = ClosePrice;
                                        NewProdStockPrice.Volume = Volume;

                                        dbProd.ProdTrnStockPrices.InsertOnSubmit(NewProdStockPrice);
                                        dbProd.SubmitChanges();

                                        //Update Latest Quote Date and No. of Years
                                        var UpdateSymbol = from d in dbProd.ProdMstSymbols where d.Symbol == Symbol select d;
                                        if (UpdateSymbol.Any())
                                        {
                                            ProdMstSymbol UpdatedSymbol = UpdateSymbol.FirstOrDefault();
                                            UpdatedSymbol.LatestQuoteDate = QuoteDate;

                                            //if (UpdateNoOfYears.ToUpper() == "Y")
                                            //{
                                            //    DateTime FirstQuoteDate = UpdatedSymbol.ProdTrnStockPrices.Min(p => p.QuoteDate);
                                            //    int NoOfYears = UpdatedSymbol.LatestQuoteDate.Value.Year - FirstQuoteDate.Year;
                                            //    UpdatedSymbol.NoOfYears = NoOfYears;
                                            //}

                                            dbProd.SubmitChanges();
                                        }

                                        updateScreenerData = true;
                                        sendAlert = true;
                                    }
                                    break;
                                }
                                else
                                {
                                    //CloudDBDataContext db = new CloudDBDataContext();
                                    //CloudTrnStockPrice NewStockPrice = new CloudTrnStockPrice();

                                    //if (db.CloudMstSymbols.Where(d => d.Symbol == Symbol && d.Exchange == Exchange).Count() > 0)
                                    //{
                                    //    NewStockPrice.SymbolId = db.CloudMstSymbols.Where(d => d.Symbol == Symbol && d.Exchange == Exchange).FirstOrDefault().Id;
                                    //    NewStockPrice.Symbol = Symbol;
                                    //    NewStockPrice.QuoteDate = QuoteDate;
                                    //    NewStockPrice.OpenPrice = OpenPrice;
                                    //    NewStockPrice.HighPrice = HighPrice;
                                    //    NewStockPrice.LowPrice = LowPrice;
                                    //    NewStockPrice.ClosePrice = ClosePrice;
                                    //    NewStockPrice.Volume = Volume;

                                    //    db.CloudTrnStockPrices.InsertOnSubmit(NewStockPrice);
                                    //    db.SubmitChanges();
                                    //}
                                    //break;
                                }
                            }
                            catch
                            {
                                Console.WriteLine("Link failed.  Retrying...");
                                Thread.Sleep(1000);
                            }
                        }
                        counter++;
                    }

                    // Close the file
                    choosenFile.Close();

                    // Delete the file
                    File.Delete("C:\\eoddata\\download\\" + Exchange + "\\" + file.Name);

                    // Inform the system that we are ready to accept new file
                    checkingFlag = true;

                } // for

                if (checkingFlag == true)
                {
                    if (DBType.ToUpper() == "PROD" && updateScreenerData == true) UpdateSymbolScreenerData(DBType, Exchange, UpdateNoOfYears);
                    if (sendAlert == true) SendAlert();
                    Console.WriteLine("Checking " + Exchange + "...");
                    checkingFlag = false;
                    updateScreenerData = false;
                    sendAlert = false;
                }

                // Process every 5 sec only to free up processor
                Thread.Sleep(5000);

            } // while

        }
    }
}
