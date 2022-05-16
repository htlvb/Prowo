using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Prowo.BlazorServer.Database
{
    public class ProjectStore : IDisposable
    {
        private readonly CosmosClient cosmosClient;

        private Container ProjectContainer => cosmosClient
            .GetDatabase("ProjectsDB")
            .GetContainer("Project");

        public ProjectStore(CosmosClient cosmosClient)
        {
            this.cosmosClient = cosmosClient;
        }

        public async IAsyncEnumerable<DbProject> GetAll()
        {
            var query = ProjectContainer.GetItemQueryIterator<DbProject>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                foreach (var project in response)
                {
                    yield return project;
                }
            }
        }

        public async Task<DbProject> Get(string projectId)
        {
            return await ProjectContainer
                .ReadItemAsync<DbProject>(projectId, new PartitionKey(projectId));
        }

        public async Task CreateOrUpdate(DbProject project)
        {
            await ProjectContainer.UpsertItemAsync(project);
        }

        public void Dispose()
        {
            cosmosClient.Dispose();
        }
    }
}
