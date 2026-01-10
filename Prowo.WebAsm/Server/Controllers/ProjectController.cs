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
        private readonly IProjectStore projectStore;
        private readonly IUserStore userStore;
        private readonly IAuthorizationService authService;
        private readonly IRegistrationStrategy registrationStrategy;

        private static DateOnly MinDate => DateOnly.FromDateTime(DateTime.Today);

        private string UserId => HttpContext.User.GetObjectId()!;

        public ProjectController(
            IProjectStore projectStore,
            IUserStore userStore,
            IAuthorizationService authService,
            IRegistrationStrategy registrationStrategy)
        {
            this.projectStore = projectStore;
            this.userStore = userStore;
            this.authService = authService;
            this.registrationStrategy = registrationStrategy;
        }

        [HttpGet("")]
        public async Task<ProjectListDto> GetProjectList()
        {
            var projectDtos = await GetProjects();
            var canCreateProject = (await authService.AuthorizeAsync(HttpContext.User, "CreateProject")).Succeeded;
            var canCreateReport = (await authService.AuthorizeAsync(HttpContext.User, "CreateReport")).Succeeded;
            return new ProjectListDto(
                projectDtos,
                new ProjectListLinksDto(
                    canCreateReport && projectDtos.Count > 0 ? "projects/all-attendees" : default,
                    canCreateProject ? "projects/new" : default,
                    canCreateProject ? "projects/duplicate" : default
                )
            );
        }

        private async Task<List<ProjectDto>> GetProjects()
        {
            var projects = (await projectStore.GetAllSince(MinDate.ToDateTime(TimeOnly.MinValue)).ToList())
                .GroupBy(v => v.Date).OrderBy(v => v.Key).SelectMany(v => v) // Sort by date, but don't change order of projects with same date
                .ToList();

            var selfAsAttendee = await userStore.GetSelfAsProjectAttendee();
            var registrationActions = registrationStrategy.GetRegistrationActions(selfAsAttendee, projects);
            
            var projectDtos = new List<ProjectDto>();
            foreach (var project in projects)
            {
                var projectDto = await GetProjectDtoFromProject(project, registrationActions[project]);
                projectDtos.Add(projectDto);
            }

            return projectDtos;
        }

        [HttpGet("templates")]
        public async Task<IEnumerable<ProjectToDuplicateDto>> GetProjectsToDuplicate()
        {
            return (await projectStore.GetAllSince(DateTime.MinValue).ToList())
                .OrderBy(p => p.CoOrganizers.Select(v => v.Id).Concat([p.Organizer.Id]).Contains(UserId) ? 1 : 2)
                .ThenByDescending(v => v.Date)
                .Select(p =>
                {
                    return new ProjectToDuplicateDto(
                        $"/projects/edit/{p.Id}?duplicate=true",
                        p.Title,
                        p.Organizer.ShortName,
                        [..p.CoOrganizers.Select(v => v.ShortName)],
                        p.Date
                    );
                });
        }

        [HttpPost("{projectId}/register")]
        [Authorize(Policy = "AttendProject")]
        public async Task<IActionResult> RegisterForProject(string projectId)
        {
            var projects = await projectStore.GetAllSince(MinDate.ToDateTime(TimeOnly.MinValue)).ToListAsync();
            var project = projects.Find(v => v.Id == projectId);
            if (project == null)
            {
                return NotFound("Project doesn't exist or is too old.");
            }
            var attendee = await userStore.GetSelfAsProjectAttendee();
            var registrationActions = registrationStrategy.GetRegistrationActions(attendee, projects)[project];
            if (!registrationActions.CanRegister)
            {
                return BadRequest("Project registration strategy doesn't allow registration.");
            }

            await projectStore.AddAttendee(projectId, attendee);
            return Ok(await GetProjects());
        }

        [HttpPost("{projectId}/deregister")]
        public async Task<IActionResult> DeregisterCurrentUserFromProject(string projectId)
        {
            var projects = await projectStore.GetAllSince(MinDate.ToDateTime(TimeOnly.MinValue)).ToListAsync();
            var project = projects.Find(v => v.Id == projectId);
            if (project == null)
            {
                return NotFound("Project doesn't exist or is too old.");
            }
            var attendee = await userStore.GetSelfAsProjectAttendee();
            var registrationActions = registrationStrategy.GetRegistrationActions(attendee, projects)[project];
            if (!registrationActions.CanDeregister)
            {
                return BadRequest("Project registration strategy doesn't allow deregistration.");
            }

            await projectStore.RemoveAttendee(projectId, attendee.Id);
            return Ok(await GetProjects());
        }

        [HttpDelete("{projectId}/attendees/{userId}")]
        public async Task<IActionResult> DeregisterUserFromProject(string projectId, string userId)
        {
            var project = await projectStore.Get(projectId);
            if (project == null || project.Date < MinDate)
            {
                return NotFound("Project doesn't exist or is too old.");
            }
            if (!(await authService.AuthorizeAsync(HttpContext.User, project, "UpdateProject")).Succeeded)
            {
                return Forbid();
            }
            await projectStore.RemoveAttendee(projectId, userId);
            return Ok();
        }

        [HttpGet("edit/{projectId}")]
        public async Task<IActionResult> GetProject(string projectId, [FromQuery]bool duplicate = false)
        {
            var (organizerCandidates, coOrganizerCandidates) = await GetOrganizerCandidates();

            Project? project = null;
            if (projectId != "new")
            {
                project = await projectStore.Get(projectId);
            }
            
            var isCreating = projectId == "new" || duplicate;
            if (isCreating)
            {
                if (!(await authService.AuthorizeAsync(HttpContext.User, "CreateProject")).Succeeded)
                {
                    return Forbid();
                }
            }
            else
            {
                if (project == null || project.Date < MinDate)
                {
                    return NotFound("Project doesn't exist or is too old.");
                }
                if (!(await authService.AuthorizeAsync(HttpContext.User, project, "UpdateProject")).Succeeded)
                {
                    return Forbid();
                }
            }

            if (project == null)
            {
                var result = new EditingProjectDto(
                    new EditingProjectDataDto(
                        "",
                        "",
                        "",
                        UserId,
                        Array.Empty<string>(),
                        DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
                        TimeOnly.FromTimeSpan(TimeSpan.FromHours(9)),
                        TimeOnly.FromTimeSpan(TimeSpan.FromHours(13)),
                        DateTime.UtcNow.Date.AddDays(7).ToUserTime(),
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
                var organizerId = organizerCandidates.Select(v => v.Id).Contains(project.Organizer.Id)
                    ? project.Organizer.Id
                    : UserId;
                var coOrganizerIds = project.CoOrganizers
                    .Where(p => coOrganizerCandidates.Select(v => v.Id).Contains(p.Id))
                    .Select(v => v.Id)
                    .ToArray();
                var date = duplicate
                    ? DateOnly.FromDateTime(DateTime.Today.AddDays(14))
                    : project.Date;
                var closingDate = duplicate
                    ? DateTime.UtcNow.Date.AddDays(7).ToUserTime()
                    : project.ClosingDate.ToUserTime();
                var saveUrl = duplicate
                    ? Url.Action(nameof(CreateProject))
                    : Url.Action(nameof(UpdateProject), new { projectId = project.Id });
                var result = new EditingProjectDto(
                    new EditingProjectDataDto(
                        project.Title,
                        project.Description,
                        project.Location,
                        organizerId,
                        coOrganizerIds,
                        date,
                        project.StartTime,
                        project.EndTime,
                        closingDate,
                        project.MaxAttendees
                    ),
                    organizerCandidates,
                    coOrganizerCandidates,
                    new EditingProjectLinksDto(saveUrl)
                );
                return Ok(result);
            }
        }

        private async Task<(IReadOnlyList<ProjectOrganizerDto> coOrganizerCandidates, IReadOnlyList<ProjectOrganizerDto> organizerCandidates)> GetOrganizerCandidates()
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
                    .Where(v => v.Id == UserId)
                    .ToList();
            }

            return (organizerCandidates, coOrganizerCandidates);
        }

        [HttpPost("")]
        public async Task<IActionResult> CreateProject([FromBody]EditingProjectDataDto projectData)
        {
            var organizerCandidates = await GetOrganizerCandidatesDictionary();
            if (!Project.TryCreateFromEditingProjectDataDto(projectData, Guid.NewGuid().ToString(), organizerCandidates, out var project, out var errorMessage))
            {
                return BadRequest(errorMessage);
            }
            if (!(await authService.AuthorizeAsync(HttpContext.User, project, "CreateProject")).Succeeded)
            {
                return Forbid();
            }
            await projectStore.CreateProject(project);
            return Ok();
        }

        [HttpPost("{projectId}")]
        public async Task<IActionResult> UpdateProject(string projectId, [FromBody] EditingProjectDataDto projectData)
        {
            var existingProject = await projectStore.Get(projectId);
            if (existingProject == null || existingProject.Date < MinDate)
            {
                return NotFound("Project doesn't exist or is too old.");
            }

            var organizerCandidates = await GetOrganizerCandidatesDictionary();
            if (!Project.TryCreateFromEditingProjectDataDto(projectData, projectId, organizerCandidates, out var project, out var errorMessage))
            {
                return BadRequest(errorMessage);
            }
            if (!(await authService.AuthorizeAsync(HttpContext.User, project, "UpdateProject")).Succeeded)
            {
                return Forbid();
            }
            await projectStore.UpdateProject(project);
            return Ok();
        }

        [HttpDelete("{projectId}")]
        public async Task<IActionResult> DeleteProject(string projectId)
        {
            var existingProject = await projectStore.Get(projectId);
            if (existingProject == null || existingProject.Date < MinDate)
            {
                return NotFound("Project doesn't exist or is too old.");
            }

            if (!(await authService.AuthorizeAsync(HttpContext.User, existingProject, "DeleteProject")).Succeeded)
            {
                return Forbid();
            }
            await projectStore.Delete(projectId);
            return Ok();
        }

        [HttpGet("{projectId}/attendees")]
        [Authorize(Policy = "CreateReport")]
        public async Task<IActionResult> GetProjectAttendees(string projectId)
        {
            var project = await projectStore.Get(projectId);
            if (project == null || project.Date < MinDate)
            {
                return NotFound("Project doesn't exist or is too old.");
            }

            var canDeregisterUsers = (await authService.AuthorizeAsync(HttpContext.User, project, "UpdateProject")).Succeeded;

            var attendees = Enumerable
                .Concat(
                    project.RegisteredAttendees.Select((v, i) => new { Attendee = v, IsWaiting = false }),
                    project.WaitingAttendees.Select((v, i) => new { Attendee = v, IsWaiting = true })
                )
                .Select(v =>
                {
                    var deregisterUrl = canDeregisterUsers ? Url.Action(nameof(DeregisterUserFromProject), new { projectId = project.Id, userId = v.Attendee.Id }) : default;
                    return new ProjectAttendeeDto(v.Attendee.FirstName, v.Attendee.LastName, v.Attendee.Class, v.Attendee.MailAddress, v.IsWaiting, deregisterUrl);
                })
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
            var projects = await projectStore.GetAllSince(MinDate.ToDateTime(TimeOnly.MinValue)).ToList();
            var canDeregisterUserTasks = projects
                .Select(async p => new { Project = p, CanDeregisterUsers = (await authService.AuthorizeAsync(HttpContext.User, p, "UpdateProject")).Succeeded });
            var canDeregisterUsers = await Task.WhenAll(canDeregisterUserTasks);
            var canDeregisterUserLookup =
                canDeregisterUsers.ToDictionary(v => v.Project, v => v.CanDeregisterUsers);
            var projectsByUserAndDate = projects
                .SelectMany(p =>
                {
                    var time = p.EndTime.HasValue ? $"{p.StartTime} - {p.EndTime}" : $"{p.StartTime}";
                    var projectDisplayName = $"{time}: {p.Title} ({p.Location})";
                    var showProjectAttendeesLink = $"projects/attendees/{p.Id}";
                    var canDeregisterUsersOfProject = canDeregisterUserLookup[p];
                    return Enumerable
                        .Concat(
                            p.RegisteredAttendees.Select(v => new { Attendee = v, IsWaiting = false }),
                            p.WaitingAttendees.Select(v => new { Attendee = v, IsWaiting = true })
                        )
                        .Select(v => new
                        {
                            Key = new
                            {
                                UserId = v.Attendee.Id,
                                ProjectDate = p.Date
                            },
                            Value = new StudentProjectDto(
                                p.Title,
                                projectDisplayName,
                                v.IsWaiting,
                                showProjectAttendeesLink,
                                canDeregisterUsersOfProject ? Url.Action(nameof(DeregisterUserFromProject), new { projectId = p.Id, userId = v.Attendee.Id }) : default
                            )
                        });
                })
                .ToLookup(v => v.Key, v => v.Value);
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
                            return new StudentDto(a.FirstName, a.LastName, a.MailAddress, projectsPerDate);
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

        private async Task<ProjectDto> GetProjectDtoFromProject(Project project, ProjectRegistrationActions registrationActions)
        {
            var userRole = project.GetUserRole(UserId);
            var canRegister =
                registrationActions.CanRegister &&
                (await authService.AuthorizeAsync(HttpContext.User, "AttendProject")).Succeeded;
            var canDeregister = registrationActions.CanDeregister;
            var canUpdate = (await authService.AuthorizeAsync(HttpContext.User, project, "UpdateProject")).Succeeded;
            var canDelete = (await authService.AuthorizeAsync(HttpContext.User, project, "DeleteProject")).Succeeded;
            var canShowAttendees = (await authService.AuthorizeAsync(HttpContext.User, project, "CreateReport")).Succeeded;
            return new ProjectDto(
                project.Title,
                project.Description,
                project.Location,
                project.Organizer.ToDto(),
                project.CoOrganizers.Select(v => v.ToDto()).ToArray(),
                project.Date,
                project.StartTime,
                project.EndTime,
                project.ClosingDate,
                project.ClosingDate.ToUserTime(),
                project.AllAttendees.Count,
                project.MaxAttendees,
                userRole.ToDto(),
                new ProjectLinksDto(
                    canRegister ? Url.Action(nameof(RegisterForProject), new { projectId = project.Id }) : default,
                    canDeregister ? Url.Action(nameof(DeregisterCurrentUserFromProject), new { projectId = project.Id }) : default,
                    canUpdate ? $"projects/edit/{project.Id}" : default,
                    canDelete ? Url.Action(nameof(DeleteProject), new { projectId = project.Id }) : default,
                    canShowAttendees ? $"projects/attendees/{project.Id}" : default
                )
            );
        }
    }
}
