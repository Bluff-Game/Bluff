using System;

namespace Bluff.Core.AI
{
    public static class Probability
    {
        /// <summary>C(n, k) sebagai double (cukup presisi untuk dek 52 kartu).</summary>
        public static double Choose(int n, int k)
        {
            if (k < 0 || n < 0 || k > n)
                return 0.0;
            k = Math.Min(k, n - k);
            double result = 1.0;
            for (int i = 1; i <= k; i++)
                result = result * (n - k + i) / i;
            return result;
        }

        /// <summary>
        /// Peluang mendapat minimal <paramref name="atLeast"/> kartu "cocok" saat mengambil <paramref name="draws"/> kartu
        /// dari populasi <paramref name="population"/> yang berisi <paramref name="successes"/> kartu cocok.
        /// </summary>
        public static double HypergeometricAtLeast(int population, int successes, int draws, int atLeast)
        {
            if (atLeast <= 0)
                return 1.0;
            population = Math.Max(0, population);
            successes = Math.Max(0, Math.Min(successes, population));
            draws = Math.Max(0, Math.Min(draws, population));
            if (successes < atLeast || draws < atLeast)
                return 0.0;

            double total = Choose(population, draws);
            if (total <= 0.0)
                return 0.0;
            double sum = 0.0;
            int max = Math.Min(successes, draws);
            for (int i = atLeast; i <= max; i++)
                sum += Choose(successes, i) * Choose(population - successes, draws - i);
            return Math.Min(1.0, sum / total);
        }
    }
}
