// 2020-04-04 DealCloud Skills Test
// Chris Hough (chris@vocl.io)

/*
 * This is my completed attempt at the coding exercise. I started in Go, but didn't get far in the 60min, so I decided to
 * switch to C# to try it out. Go easy on me, this is my first experience writing C#.
 *
 * I decided to use an existing framework (https://github.com/LutsenkoKirill/AlphaVantage.Net) though, looking back,
 * it probably would have been easier just to do the direct calls and process the results.
 *
 * Obviously, there are a lot of things I would do differently if this was 'real' code, I'm not catching any exceptions or data valaidations.
 * There are a lot of loops, those would be revisted if I were approaching this from an efficiency perspective.
 *
 * The full process runs into the 5 calls/minute limit on the Alpha Vantage API, so there is a sleep before the last solution
 *
 * To run, add your api key
 *
*/


using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using AlphaVantage.Net.Stocks;
using AlphaVantage.Net.Stocks.TimeSeries;


namespace skilltest
{
    class MainClass
    {
        public static async Task Main(string[] args)
        {
            // Find the average volume of MSFT in the past 7 days
            await AverageVolumeAsync("MSFT", DateTime.Now, 7);

            // Find the highest closing price of AAPL in the past 6 months
            await HighestClosingPrice("AAPL", DateTime.Now, 6);

            // Find the difference between open and close price for BA for every day in the last month
            await DailyPriceDiff("BA", DateTime.Now, 1);

            // Already used up 3 calls within 1min, rest for a minute before proceeding.
            await Task.Delay(60000);

            // Given a list of stock symbols, find the symbol with the largest return over the past month
            string[] symbols = { "MSFT", "AAPL", "BA" };
            await MaxMonthReturn(symbols);
        }

        public static async Task<List<StockDataPoint>> GetApiResults(string symbol, bool compact)
        {
            // Need to add your own API key
            string apiKey = "";

            if(apiKey == "")
            {
                Console.WriteLine("Please add your API key to the apiKey variable in 'GetApiResults'");
                System.Environment.Exit(0);
            }

            var client = new AlphaVantageStocksClient(apiKey);
            StockTimeSeries timeSeries = await client.RequestDailyTimeSeriesAsync(symbol, compact ? TimeSeriesSize.Compact : TimeSeriesSize.Full, adjusted: false);

            // convert the collection to a list, this assumes the results are ordered by date
            List<StockDataPoint> quotes = timeSeries.DataPoints.ToList();

            return quotes;
        }

        // for the passed range in days, calculate the average volume
        public static async Task AverageVolumeAsync(string symbol, DateTime upperLimit, int rangeDays)
        {
            long volumeSum = 0;
            // this will get 'days' of available data, not calendar days (or even trade days)
            int i = 0;

            List<StockDataPoint> quotes = await GetApiResults(symbol, true);

            // loop through the results
            foreach(var quote in quotes)
            {
                // if we're in the passed range, add the volume to the sum
                if(quote.Time <= upperLimit)
                {
                    volumeSum += quote.Volume;
                    i++;
                }
                if(i == rangeDays)
                {
                    break;
                }
            }

            // calculate the average volume
            Console.WriteLine($"Average {rangeDays} day volume for {symbol}: " + volumeSum / rangeDays);
        }

        // for the symbol passed, get teh higest closing price in the range
        public static async Task HighestClosingPrice(string symbol, DateTime upperLimt, int rangeMonths)
        {
            decimal maxClose = 0;

            // This won't get the exact month, but the days. Would probably use a search for calendar values
            DateTime lowerLimt = upperLimt.AddMonths(rangeMonths * -1);

            List<StockDataPoint> quotes = await GetApiResults(symbol, false);

            // loop through each of the returned results
            foreach(var quote in quotes)
            {
                // if we reach the lower range limit, break
                if(quote.Time < lowerLimt)
                {
                    break;
                }
                // if we're in the passed range, updated the max close price if applicable
                if(quote.Time <= upperLimt)
                {
                    if(quote.ClosingPrice > maxClose)
                    {
                        maxClose = quote.ClosingPrice;
                    }
                }
            }

            Console.WriteLine($"Maximum {rangeMonths} month closing price for {symbol}: " + maxClose);
        }

        // for the symbol passed, get the open/close difference in the range passed
        public static async Task DailyPriceDiff(string symbol, DateTime upperLimt, int rangeMonths)
        {
            // This won't get the exact month, but the days. Would probably use a search for calendar values
            DateTime lowerLimt = upperLimt.AddMonths(rangeMonths * -1);

            // need the full data set for 6 months
            List<StockDataPoint> quotes = await GetApiResults(symbol, false);

            // loop through the result set and output if within the given range
            foreach (var quote in quotes)
            {
                if (quote.Time < lowerLimt)
                {
                    break;
                }
                if (quote.Time <= upperLimt)
                {
                    Console.WriteLine($"On {quote.Time.ToShortDateString()}, open/close variance was {quote.ClosingPrice - quote.OpeningPrice}");
                }
            }
        }

        // Not sure if the question is asking for gain in last calendar month or month/days. I went with the latter
        public static async Task<decimal?> RangeReturn(string symbol, DateTime upperLimt, int rangeMonths)
        {
            decimal? maxClose = null;
            decimal? firstClose = null;

            DateTime lowerLimt = upperLimt.AddMonths(rangeMonths * -1);

            // Assuming range of less than 3 months, get limited result set
            List<StockDataPoint> quotes = await GetApiResults(symbol, true);

            // loop through the individual sequrity list
            foreach (var quote in quotes)
            {
                // if we've hit the lower bound (month) of the requested timeframe, break out of the loop
                if (quote.Time < lowerLimt)
                {
                    break;
                }
                // if we're within the requested timeframe, get to work
                if (quote.Time <= upperLimt)
                {
                    // Since we're looping through backwards to get the earliest close, always set the starting close
                    firstClose = quote.ClosingPrice;

                    // Yuck. If we're at the start of the range, set the close
                    if(!maxClose.HasValue)
                    {
                        maxClose = quote.ClosingPrice;
                    }
                }
            }
            return maxClose - firstClose;
        }

        // For each of the passed symbols, get the security with the maximum return in the past month
        public static async Task MaxMonthReturn(string[] symbols)
        {
            DateTime upperLimit = DateTime.Now;
            int rangeMonths = 1;
            decimal? maxReturn = null;
            string maxSymbol = "";
            decimal? symbolReturn;

            // run through the symbol list getting each security's monthly return, setting max
            foreach (var symbol in symbols)
            {
                symbolReturn = await RangeReturn(symbol, upperLimit, rangeMonths);
                //Console.WriteLine($"{symbol} return for {rangeMonths} month was {symbolReturn}");
                // set the max value if applicable
                if((!maxReturn.HasValue) || (symbolReturn > maxReturn))
                {
                    maxSymbol = symbol;
                    maxReturn = symbolReturn;
                }
            }

            Console.WriteLine($"{maxSymbol} had the largest return for {rangeMonths} month with {maxReturn}");
        }
    }

}
