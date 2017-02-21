using System;
using System.Collections.Generic;

namespace Testing
{
    public static class Extensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                int k = (new Random()).Next(n--);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
