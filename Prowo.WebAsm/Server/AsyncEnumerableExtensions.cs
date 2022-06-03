namespace Prowo.WebAsm.Server
{
    public static class AsyncEnumerableExtensions
    {
        public static async IAsyncEnumerable<TOut> Select<TIn, TOut>(this IAsyncEnumerable<TIn> list, Func<TIn, TOut> fn)
        {
            await foreach (var item in list)
            {
                yield return fn(item);
            }
        }

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
