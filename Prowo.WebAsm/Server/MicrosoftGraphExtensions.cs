using System.Threading.Channels;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Serialization;

namespace Prowo.WebAsm.Server;

public static class MicrosoftGraphExtensions
{
    public static IAsyncEnumerable<TEntity> ReadAll<TEntity, TCollectionPage>(
        this GraphServiceClient client,
        Task<TCollectionPage?> query)
        where TCollectionPage : IParsable, IAdditionalDataHolder, new()
    {
        var channel = Channel.CreateUnbounded<TEntity>();

        Task.Run(async () =>
        {
            try
            {
                await PageIterator<TEntity, TCollectionPage?>
                    .CreatePageIterator(
                        client,
                        await query,
                        async item =>
                        {
                            await channel.Writer.WriteAsync(item);
                            return true; // continue iteration
                        })
                    .IterateAsync();
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                channel.Writer.Complete(ex);
            }
        });
        return channel.Reader.ReadAllAsync();
    }
}