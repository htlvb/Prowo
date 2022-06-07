using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

string connectionString = configuration.GetConnectionString("CosmosDb");
using CosmosClient cosmosClient = new(connectionString);

//await TestCollectionOperations();
//await UpdateClosingDate();

async Task TestCollectionOperations()
{
    var containerResponse = await cosmosClient
        .GetDatabase("ProjectsDB")
        .DefineContainer("ConsoleTest", "/id")
        .CreateAsync();
    var container = containerResponse.Container;
    var itemResponse = await container.CreateItemAsync(
        new Item("1111", Array.Empty<string>())
    );
    var patch1ItemResponse = await container.PatchItemAsync<Item>(
        itemResponse.Resource.id,
        new PartitionKey(itemResponse.Resource.id),
        new[]
        {
            PatchOperation.Add("/items/-", "Johannes"),
            PatchOperation.Add("/items/-", "Sylvia")
        }
    );
    var patch2ItemResponse = await container.PatchItemAsync<Item>(
        itemResponse.Resource.id,
        new PartitionKey(itemResponse.Resource.id),
        new[]
        {
            PatchOperation.Add("/items/-", "Marie"),
            PatchOperation.Add("/items/-", "Theo"),
            PatchOperation.Add("/items/-", "Sarah")
        }
    );
    var patch3ItemResponse = await container.PatchItemAsync<Item>(
        itemResponse.Resource.id,
        new PartitionKey(itemResponse.Resource.id),
        new[]
        {
            PatchOperation.Remove("/items/1")
        },
        new PatchItemRequestOptions { FilterPredicate = $"FROM p WHERE p.items[1] = '{"Sylvia"}'" }
    );

    var patch4ItemResponse = await container.PatchItemAsync<Item>(
        itemResponse.Resource.id,
        new PartitionKey(itemResponse.Resource.id),
        new[]
        {
            PatchOperation.Remove("/items/1")
        },
        new PatchItemRequestOptions { FilterPredicate = $"FROM p WHERE p.items[1] = '{"Marie"}'" }
    );

    try
    {
        var patch5ItemResponse = await container.PatchItemAsync<Item>(
            itemResponse.Resource.id,
            new PartitionKey(itemResponse.Resource.id),
            new[]
            {
                PatchOperation.Remove("/items/1")
            },
            new PatchItemRequestOptions { FilterPredicate = $"FROM p WHERE p.items[1] = '{"theo"}'" }
        );
    }
    catch (CosmosException e) when (e.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
    {
        Console.WriteLine("Can't find theo");
    }

    var deleteContainerResponse = await container.DeleteContainerAsync();

}

async Task UpdateClosingDate()
{
    var container = cosmosClient.GetContainer("ProjectsDB", "Project");

    var query = container.GetItemQueryIterator<DbProject>();
    while (query.HasMoreResults)
    {
        var response = await query.ReadNextAsync();
        foreach (var dbProject in response)
        {
            if (dbProject.closingDate.TimeOfDay == TimeSpan.Zero)
            {
                Console.WriteLine($"{dbProject.id} - {dbProject.closingDate} ({dbProject.closingDate.Kind})");
                await container.PatchItemAsync<DbProject>(
                    dbProject.id,
                    new PartitionKey(dbProject.id),
                    new[]
                    {
                        PatchOperation.Replace("/closingDate", dbProject.closingDate.AddHours(-2)),
                    }
                );
            }
        }
    }
}

record Item(string id, string[] items);

record DbProject(string id, DateTime closingDate);