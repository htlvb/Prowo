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
            await ProjectContainer.PatchItemAsync<DbProject>(
                project.Id,
                new PartitionKey(project.Id),
                new[]
                {
                    PatchOperation.Replace("/title", project.Title),
                    PatchOperation.Replace("/description", project.Description),
                    PatchOperation.Replace("/location", project.Location),
                    PatchOperation.Replace("/organizerId", project.OrganizerId),
                    PatchOperation.Replace("/coOrganizerIds", project.CoOrganizerIds),
                    PatchOperation.Replace("/date", project.Date),
                    PatchOperation.Replace("/startTime", project.StartTime),
                    PatchOperation.Replace("/endTime", project.EndTime),
                    PatchOperation.Replace("/maxAttendees", project.MaxAttendees)
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
            var response = await ProjectContainer.PatchItemAsync<DbProject>(
                projectId,
                new PartitionKey(projectId),
                new[]
                {
                    PatchOperation.Add("/registrationEvents/-", registrationEvent)
                }
            );
            return response.Resource;
        }

        public async Task<DbProject> RemoveAttendee(string projectId, string userId)
        {
            DbProject.RegistrationEvent registrationEvent = new()
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Action = DbProject.RegistrationAction.Deregister
            };
            var response = await ProjectContainer.PatchItemAsync<DbProject>(
                projectId,
                new PartitionKey(projectId),
                new[]
                {
                    PatchOperation.Add("/registrationEvents/-", registrationEvent)
                }
            );
            return response.Resource;
        }

        public void Dispose()
        {
            cosmosClient.Dispose();
        }
    }
}
