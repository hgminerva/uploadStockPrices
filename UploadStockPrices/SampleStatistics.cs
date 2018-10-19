using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UploadStockPrices
{
    public class SampleStatistics
    {
        public double sum(double[] x)
        {
            double value = 0;
            for (int i = 0; i < x.Length; i++)
            {
                value += x[i];
            }
            return value;
        }

        public double? mean(double[] x)
        {
            if (x.Length == 0) return null;
            return sum(x) / x.Length;
        }

        public double? geometric_mean(double[] x)
        {
            if (x.Length == 0) return null;

            double value = 1;

            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] <= 0) return null;
                value *= x[i];
            }

            return Math.Pow(value, 1 / x.Length);
        }

        public double? harmonic_mean(double[] x)
        {
            if (x.Length == 0) return null;

            double reciprocal_sum = 0;

            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] <= 0) return null;
                reciprocal_sum += 1 / x[i];
            }

            return x.Length / reciprocal_sum;
        }

        public double? root_mean_square(double[] x)
        {
            if (x.Length == 0) return null;

            double sum_of_squares = 0;

            for (int i = 0; i < x.Length; i++)
            {
                sum_of_squares += Math.Pow(x[i], 2);
            }

            return Math.Sqrt(sum_of_squares / x.Length);
        }

        public double min(double[] x)
        {
            double value = 0;
            for (var i = 0; i < x.Length; i++)
            {
                if (x[i] < value) value = x[i];
            }
            return value;
        }

        public double max(double[] x)
        {
            double value = 0;
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] > value) value = x[i];
            }
            return value;
        }

        // http://en.wikipedia.org/wiki/Variance
        public double? variance(double[] x)
        {
            if (x.Length == 0) return null;

            double mean_value = (double)mean(x);
            List<double> deviations = new List<double>();

            for (int i = 0; i < x.Length; i++)
            {
                deviations.Add(Math.Pow(x[i] - mean_value, 2));
            }
            return mean(deviations.ToArray());
        }

        // http://en.wikipedia.org/wiki/Standard_deviation
        public double? standard_deviation(double[] x)
        {
            if (x.Length == 0) return null;

            return Math.Sqrt((double)variance(x));
        }

        public double sum_nth_power_deviations(double[] x, int n)
        {
            double mean_value = (double)mean(x);
            double sum = 0;

            for (int i = 0; i < x.Length; i++)
            {
                sum += Math.Pow(x[i] - mean_value, n);
            }

            return sum;
        }

        // http://en.wikipedia.org/wiki/Variance
        public double? sample_variance(double[] x)
        {
            if (x.Length <= 1) return null;

            double sum_squared_deviations_value = sum_nth_power_deviations(x, 2);

            return sum_squared_deviations_value / (x.Length - 1);
        }

        // http://en.wikipedia.org/wiki/Standard_deviation
        public double? sample_standard_deviation(double[] x)
        {
            if (x.Length <= 1) return null;

            return Math.Sqrt((double)sample_variance(x));
        }

        // http://en.wikipedia.org/wiki/Covariance
        public double? sample_covariance(double[] x, double[] y)
        {
            if (x.Length <= 1 || x.Length != y.Length)
            {
                return null;
            }

            double xmean = (double)mean(x);
            double ymean = (double)mean(y);
            double sum = 0;

            for (var i = 0; i < x.Length; i++)
            {
                sum += (x[i] - xmean) * (y[i] - ymean);
            }

            return sum / (x.Length - 1);
        }

        // http://en.wikipedia.org/wiki/Correlation_and_dependence
        public double? sample_correlation(double[] x, double[] y)
        {
            double cov = (double)sample_covariance(x, y);
            double xstd = (double)sample_standard_deviation(x);
            double ystd = (double)sample_standard_deviation(y);

            if (cov == null || xstd == null || ystd == null)
            {
                return null;
            }

            return cov / xstd / ystd;
        }
    }
}
