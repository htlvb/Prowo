using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Shared;

namespace Prowo.WebAsm.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/projects")]
    public class ProjectController : ControllerBase
    {
        private readonly PostgresqlProjectStore postgresqlProjectStore;
        private readonly UserStore userStore;
        private readonly IAuthorizationService authService;

        private DateOnly MinDate => DateOnly.FromDateTime(DateTime.Today.AddDays(-7));

        public ProjectController(
            PostgresqlProjectStore postgresqlProjectStore,
            UserStore userStore,
            IAuthorizationService authService)
        {
            this.postgresqlProjectStore = postgresqlProjectStore;
            this.userStore = userStore;
            this.authService = authService;
        }

        [HttpGet("")]
        public async Task<ProjectListDto> GetProjectList()
        {
            var projects = (await postgresqlProjectStore.GetAllSince(MinDate.ToDateTime(TimeOnly.MinValue)).ToList())
                .GroupBy(v => v.Date).OrderBy(v => v.Key).SelectMany(v => v); // Sort by date, but don't change order of projects with same date;

            var projectDtos = new List<ProjectDto>();
            foreach (var project in projects)
            {
                var projectDto = await GetProjectDtoFromProject(project);
                projectDtos.Add(projectDto);
            }
            var canCreateProject = (await authService.AuthorizeAsync(HttpContext.User, "CreateProject")).Succeeded;
            var canCreateReport = (await authService.AuthorizeAsync(HttpContext.User, "CreateReport")).Succeeded;
            return new ProjectListDto(
                projectDtos,
                new ProjectListLinksDto(
                    canCreateReport && projectDtos.Count > 0 ? "projects/all-attendees" : default,
                    canCreateProject ? "projects/new" : default
                )
            );
        }

        [HttpPost("{projectId}/register")]
        [Authorize(Policy = "AttendProject")]
        public async Task<IActionResult> RegisterForProject(string projectId)
        {
            var attendee = await userStore.GetSelfAsProjectAttendee();
            var project = await postgresqlProjectStore.AddAttendee(projectId, attendee);
            return Ok(await GetProjectDtoFromProject(project));
        }

        [HttpPost("{projectId}/deregister")]
        public async Task<ProjectDto> DeregisterFromProject(string projectId)
        {
            var project = await postgresqlProjectStore.RemoveAttendee(projectId, HttpContext.User.GetObjectId());
            return await GetProjectDtoFromProject(project);
        }

        [HttpGet("edit/{projectId}")]
        public async Task<IActionResult> GetProject(string projectId)
        {
            IReadOnlyList<ProjectOrganizerDto> coOrganizerCandidates = (await userStore.GetOrganizerCandidates().ToList())
                .OrderBy(v => v.LastName)
                .ThenBy(v => v.FirstName)
                .Select(v => new ProjectOrganizerDto(v.Id, $"{v.LastName} {v.FirstName} ({v.ShortName})"))
                .ToList();
            IReadOnlyList<ProjectOrganizerDto> organizerCandidates;
            if ((await authService.AuthorizeAsync(HttpContext.User, "ChangeProjectOrganizer")).Succeeded)
            {
                organizerCandidates = coOrganizerCandidates;
            }
            else
            {
                organizerCandidates = coOrganizerCandidates
                    .Where(v => v.Id == HttpContext.User.GetObjectId())
                    .ToList();
            }

            if (projectId == "new")
            {
                if (!(await authService.AuthorizeAsync(HttpContext.User, "CreateProject")).Succeeded)
                {
                    return Forbid();
                }

                var result = new EditingProjectDto(
                    new EditingProjectDataDto(
                        "",
                        "",
                        "",
                        HttpContext.User.GetObjectId(),
                        Array.Empty<string>(),
                        DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
                        TimeOnly.FromTimeSpan(TimeSpan.FromHours(9)),
                        TimeOnly.FromTimeSpan(TimeSpan.FromHours(13)),
                        DateTime.UtcNow.Date.AddDays(14).ToUserTime(),
                        20
                    ),
                    organizerCandidates,
                    coOrganizerCandidates,
                    new EditingProjectLinksDto(
                        Url.Action(nameof(CreateProject))
                    )
                );
                return base.Ok(result);
            }
            else
            {
                var project = await postgresqlProjectStore.Get(projectId);
                if (project == null || project.Date < MinDate)
                {
                    return NotFound("Project doesn't exist or is too old.");
                }
                if (!(await authService.AuthorizeAsync(HttpContext.User, project, "UpdateProject")).Succeeded)
                {
                    return Forbid();
                }
                var result = new EditingProjectDto(
                    new EditingProjectDataDto(
                        project.Title,
                        project.Description,
                        project.Location,
                        project.Organizer.Id,
                        project.CoOrganizers.Select(v => v.Id).ToArray(),
                        project.Date,
                        project.StartTime,
                        project.EndTime,
                        project.ClosingDate.ToUserTime(),
                        project.MaxAttendees
                    ),
                    organizerCandidates,
                    coOrganizerCandidates,
                    new EditingProjectLinksDto(
                        Url.Action(nameof(UpdateProject), new { projectId = project.Id })
                    )
                );
                return Ok(result);
            }
        }

        [HttpPost("")]
        public async Task<IActionResult> CreateProject([FromBody]EditingProjectDataDto projectData)
        {
            var organizerCandidates = await GetOrganizerCandidatesDictionary();
            var project = GetProjectFromProjectDataDto(projectData, Guid.NewGuid().ToString(), organizerCandidates);
            if (!(await authService.AuthorizeAsync(HttpContext.User, project, "CreateProject")).Succeeded)
            {
                return Forbid();
            }
            if (project.Date < MinDate)
            {
                return BadRequest("Project too old.");
            }
            await postgresqlProjectStore.CreateProject(project);
            return Ok();
        }

        [HttpPost("{projectId}")]
        public async Task<IActionResult> UpdateProject(string projectId, [FromBody] EditingProjectDataDto projectData)
        {
            var existingProject = await postgresqlProjectStore.Get(projectId);
            if (existingProject == null || existingProject.Date < MinDate)
            {
                return NotFound("Project doesn't exist or is too old.");
            }

            var organizerCandidates = await GetOrganizerCandidatesDictionary();
            var project = GetProjectFromProjectDataDto(projectData, projectId, organizerCandidates);
            if (!(await authService.AuthorizeAsync(HttpContext.User, project, "UpdateProject")).Succeeded)
            {
                return Forbid();
            }
            if (project.Date < MinDate)
            {
                return BadRequest("Project too old.");
            }
            await postgresqlProjectStore.UpdateProject(project);
            return Ok();
        }

        [HttpGet("{projectId}/attendees")]
        [Authorize(Policy = "CreateReport")]
        public async Task<IActionResult> GetProjectAttendees(string projectId)
        {
            var project = await postgresqlProjectStore.Get(projectId);
            if (project == null || project.Date < MinDate)
            {
                return NotFound("Project doesn't exist or is too old.");
            }

            var attendees = Enumerable
                .Concat(
                    project.RegisteredAttendees.Select((v, i) => new ProjectAttendeeDto(v.FirstName, v.LastName, v.Class, IsWaiting: false)),
                    project.WaitingAttendees.Select((v, i) => new ProjectAttendeeDto(v.FirstName, v.LastName, v.Class, IsWaiting: true))
                )
                .OrderBy(v => v.IsWaiting)
                .ThenBy(v => v.Class)
                .ThenBy(v => v.LastName)
                .ThenBy(v => v.FirstName)
                .ToList();
            return Ok(new ProjectAttendeesDto(
                project.Title,
                project.Date,
                project.StartTime,
                project.EndTime,
                attendees
            ));
        }

        [HttpGet("attendees")]
        [Authorize(Policy = "CreateReport")]
        public async Task<AttendanceOverviewDto> GetAllAttendees()
        {
            var attendeeCandidates = await userStore.GetAttendeeCandidates().ToList();
            var projects = await postgresqlProjectStore.GetAllSince(MinDate.ToDateTime(TimeOnly.MinValue)).ToList();
            var projectsByUserAndDate = projects
                .SelectMany(p =>
                {
                    var time = p.EndTime.HasValue ? $"{p.StartTime} - {p.EndTime}" : $"{p.StartTime}";
                    var projectDisplayName = $"{time}: {p.Title} ({p.Location})";
                    return Enumerable.Concat(
                        p.RegisteredAttendees.Select((v, i) => new { UserId = v.Id, ProjectDisplayName = projectDisplayName, ProjectDate = p.Date, IsWaiting = false }),
                        p.WaitingAttendees.Select((v, i) => new { UserId = v.Id, ProjectDisplayName = projectDisplayName, ProjectDate = p.Date, IsWaiting = true })
                    );
                })
                .ToLookup(v => new { v.UserId, v.ProjectDate }, v => new StudentProjectDto(v.ProjectDisplayName, v.IsWaiting));
            var dates = projects
                .Select(p => p.Date)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
            var groups = attendeeCandidates
                .GroupBy(v => v.Class)
                .Select(g =>
                {
                    var students = g
                        .Select(a =>
                        {
                            var projectsPerDate = dates
                                .Select(d => new StudentProjectsAtDateDto(projectsByUserAndDate[new { UserId = a.Id, ProjectDate = d }].ToList()))
                                .ToList();
                            return new StudentDto(a.FirstName, a.LastName, projectsPerDate);
                        })
                        .OrderBy(v => v.LastName)
                        .ThenBy(v => v.FirstName)
                        .ToList();
                    return new GroupDto(g.Key, students);
                })
                .OrderBy(v => v.Name)
                .ToList();
            return new AttendanceOverviewDto(dates, groups);
        }

        private async Task<Dictionary<string, ProjectOrganizer>> GetOrganizerCandidatesDictionary()
        {
            var organizerCandidates = await userStore.GetOrganizerCandidates().ToList();
            return organizerCandidates.ToDictionary(v => v.Id);
        }

        private async Task<ProjectDto> GetProjectDtoFromProject(Project project)
        {
            ProjectOrganizerDto mapOrganizer(ProjectOrganizer organizer)
            {
                return new ProjectOrganizerDto(organizer.Id, $"{organizer.LastName} {organizer.FirstName} ({organizer.ShortName})");
            }

            UserRoleForProjectDto getCurrentUserRole(Project project)
            {
                var currentUserId = HttpContext.User.GetObjectId();
                if (project.Organizer.Id == currentUserId)
                {
                    return UserRoleForProjectDto.Organizer;
                }
                if (project.CoOrganizers.Any(v => v.Id == currentUserId))
                {
                    return UserRoleForProjectDto.CoOrganizer;
                }
                if (project.RegisteredAttendees.Any(v => v.Id == currentUserId))
                {
                    return UserRoleForProjectDto.Registered;
                }
                if (project.WaitingAttendees.Any(v => v.Id == currentUserId))
                {
                    return UserRoleForProjectDto.Waiting;
                }
                return UserRoleForProjectDto.NotRelated;
            }

            var userRole = getCurrentUserRole(project);
            var canRegister =
                userRole != UserRoleForProjectDto.Registered &&
                userRole != UserRoleForProjectDto.Waiting &&
                (await authService.AuthorizeAsync(HttpContext.User, project, "AttendProject")).Succeeded;
            var canDeregister =
                userRole == UserRoleForProjectDto.Registered ||
                userRole == UserRoleForProjectDto.Waiting;
            var canUpdate = (await authService.AuthorizeAsync(HttpContext.User, project, "UpdateProject")).Succeeded;
            var canShowAttendees = (await authService.AuthorizeAsync(HttpContext.User, project, "CreateReport")).Succeeded;
            return new ProjectDto(
                project.Title,
                project.Description,
                project.Location,
                mapOrganizer(project.Organizer),
                project.CoOrganizers.Select(mapOrganizer).ToArray(),
                project.Date,
                project.StartTime,
                project.EndTime,
                project.ClosingDate,
                project.ClosingDate.ToUserTime(),
                project.AllAttendees.Count,
                project.MaxAttendees,
                getCurrentUserRole(project),
                new ProjectLinksDto(
                    canRegister ? Url.Action(nameof(RegisterForProject), new { projectId = project.Id }) : default,
                    canDeregister ? Url.Action(nameof(DeregisterFromProject), new { projectId = project.Id }) : default,
                    canUpdate ? $"projects/edit/{project.Id}" : default,
                    canShowAttendees ? $"projects/attendees/{project.Id}" : default
                )
            );
        }

        private static Project GetProjectFromProjectDataDto(
            EditingProjectDataDto projectData,
            string projectId,
            IReadOnlyDictionary<string, ProjectOrganizer> organizerCandidates
        )
        {
            var coOrganizers = projectData.CoOrganizerIds
                .Except(new[] { projectData.OrganizerId })
                .Select(coOrganizerId =>
                    organizerCandidates.TryGetValue(coOrganizerId, out ProjectOrganizer? projectCoOrganizer)
                        ? projectCoOrganizer
                        : throw new Exception($"Co-Organizer with ID \"{coOrganizerId}\" not found")
                )
                .ToArray();
            return new Project(
                projectId,
                projectData.Title,
                projectData.Description,
                projectData.Location,
                organizerCandidates.TryGetValue(projectData.OrganizerId, out ProjectOrganizer? projectOrganizer) ? projectOrganizer : throw new Exception("Organizer not found"),
                coOrganizers,
                projectData.Date,
                projectData.StartTime,
                projectData.EndTime,
                projectData.ClosingDate.FromUserTime(),
                projectData.MaxAttendees,
                Array.Empty<ProjectAttendee>()
            );
        }
    }
}
