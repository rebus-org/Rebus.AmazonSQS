using System.Collections.Generic;
using System.Linq;

namespace Rebus.Internals
{
    static class EnumerableExtensions
    {
        public static IEnumerable<List<T>> Batch<T>(this IEnumerable<T> items, int maxItemsPerBatch)
        {
            var list = new List<T>();

            foreach (var item in items)
            {
                list.Add(item);

                if (list.Count >= maxItemsPerBatch)
                {
                    yield return list;
                    list = new List<T>();
                }
            }

            if (list.Any())
            {
                yield return list;
            }
        }
    }
}