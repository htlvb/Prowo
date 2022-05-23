using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Prowo.Web.Data
{
    public class ProjectStore : IDisposable
    {
        private readonly CosmosClient cosmosClient;

        private Container ProjectContainer => cosmosClient.GetContainer("ProjectsDB", "Project");

        public ProjectStore(CosmosClient cosmosClient)
        {
            this.cosmosClient = cosmosClient;
        }

        public async IAsyncEnumerable<Project> GetAll()
        {
            var query = ProjectContainer.GetItemQueryIterator<DbProject>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                foreach (var project in response)
                {
                    yield return project.ToProject();
                }
            }
        }

        public async Task<Project> Get(string projectId)
        {
            var dbProject = await ProjectContainer
                .ReadItemAsync<DbProject>(projectId, new PartitionKey(projectId));
            return dbProject.Resource.ToProject();
        }

        public async Task CreateProject(Project project)
        {
            await ProjectContainer.CreateItemAsync(DbProject.FromProject(project));
        }

        public async Task UpdateProject(Project project)
        {
            var dbProject = DbProject.FromProject(project);

            await ProjectContainer.PatchItemAsync<DbProject>(
                dbProject.Id,
                new PartitionKey(dbProject.Id),
                new[]
                {
                    PatchOperation.Replace("/title", dbProject.Title),
                    PatchOperation.Replace("/description", dbProject.Description),
                    PatchOperation.Replace("/location", dbProject.Location),
                    PatchOperation.Replace("/organizerId", dbProject.OrganizerId),
                    PatchOperation.Replace("/coOrganizerIds", dbProject.CoOrganizerIds),
                    PatchOperation.Replace("/date", dbProject.Date),
                    PatchOperation.Replace("/startTime", dbProject.StartTime),
                    PatchOperation.Replace("/endTime", dbProject.EndTime),
                    PatchOperation.Replace("/closingDate", dbProject.ClosingDate),
                    PatchOperation.Replace("/maxAttendees", dbProject.MaxAttendees)
                }
            );
        }

        public async Task<DbProject> AddAttendee(string projectId, ProjectAttendee attendee)
        {
            DbProject.RegistrationEvent registrationEvent = new()
            {
                UserId = attendee.UserId,
                FirstName = attendee.FirstName,
                LastName = attendee.LastName,
                Class = attendee.Class,
                Timestamp = DateTime.UtcNow,
                Action = DbProject.RegistrationAction.Register
            };
            return await ProjectContainer.PatchItemAsync<DbProject>(
                projectId,
                new PartitionKey(projectId),
                new[]
                {
                    PatchOperation.Add("/registrationEvents/-", registrationEvent)
                },
                new PatchItemRequestOptions
                {
                    FilterPredicate = $"FROM p WHERE p.closingDate > GetCurrentDateTime()"
                }
            );
        }

        public async Task<DbProject> RemoveAttendee(string projectId, string userId)
        {
            DbProject.RegistrationEvent registrationEvent = new()
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Action = DbProject.RegistrationAction.Deregister
            };
            return await ProjectContainer.PatchItemAsync<DbProject>(
                projectId,
                new PartitionKey(projectId),
                new[]
                {
                    PatchOperation.Add("/registrationEvents/-", registrationEvent)
                }
            );
        }

        public void Dispose()
        {
            cosmosClient.Dispose();
        }
    }
}
