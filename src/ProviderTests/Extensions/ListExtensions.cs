using System;
using System.Collections.Generic;

namespace Genbox.ProviderTests.Extensions
{
    public static class ListExtensions
    {
        private static readonly Random _rng = new Random();

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;

            while (n > 1)
            {
                n--;
                int k = _rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}
