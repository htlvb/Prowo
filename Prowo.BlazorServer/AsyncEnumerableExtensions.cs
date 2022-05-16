using System.Collections.Generic;
using System.Threading.Tasks;

namespace Prowo.BlazorServer
{
    public static class AsyncEnumerableExtensions
    {
        public static async Task<List<T>> ToList<T>(this IAsyncEnumerable<T> list)
        {
            List<T> result = new();
            await foreach (var item in list)
            {
                result.Add(item);
            }
            return result;
        }
    }
}
