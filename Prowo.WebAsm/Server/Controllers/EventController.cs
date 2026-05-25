using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Shared;

namespace Prowo.WebAsm.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/events")]
public class EventController : ControllerBase
{
    private readonly IEventStore eventStore;
    private readonly IProjectStore projectStore;
    private readonly IAuthorizationService authService;

    public EventController(IEventStore eventStore, IProjectStore projectStore, IAuthorizationService authService)
    {
        this.eventStore = eventStore;
        this.projectStore = projectStore;
        this.authService = authService;
    }

    [HttpGet("")]
    public async Task<EventListDto> GetEventList()
    {
        var canManage = (await authService.AuthorizeAsync(HttpContext.User, "ManageEvents")).Succeeded;
        var events = (await eventStore.GetAll().ToList())
            .OrderByDescending(e => e.Start)
            .Select(e => new EventDto(e.Id, e.Title, e.Start, e.End, e.VisibleFrom, e.RegistrationFrom))
            .ToList();
        return new EventListDto(
            events,
            new EventListLinksDto(canManage ? Url.Action(nameof(CreateEvent)) : null)
        );
    }

    [HttpGet("edit/new")]
    [Authorize(Policy = "ManageEvents")]
    public IActionResult GetNewEvent()
    {
        return Ok(new EditingEventDto(
            new EditingEventDataDto(Title: "", Start: null, End: null, VisibleFromLocalUserTime: null, RegistrationFromLocalUserTime: null),
            new EditingEventLinksDto(Url.Action(nameof(CreateEvent)))
        ));
    }

    [HttpGet("edit/{eventId}")]
    [Authorize(Policy = "ManageEvents")]
    public async Task<IActionResult> GetEvent(string eventId)
    {
        var e = await eventStore.Get(eventId);
        if (e == null) return NotFound();
        return Ok(new EditingEventDto(
            new EditingEventDataDto(e.Title, e.Start, e.End, e.VisibleFrom.ToUserTime(), e.RegistrationFrom.ToUserTime()),
            new EditingEventLinksDto(Url.Action(nameof(UpdateEvent), new { eventId = e.Id }))
        ));
    }

    [HttpPost("")]
    [Authorize(Policy = "ManageEvents")]
    public async Task<IActionResult> CreateEvent([FromBody] EditingEventDataDto data)
    {
        var errors = Validate(data);
        if (errors.Length > 0) return BadRequest(errors);
        var e = new Event(
            Guid.NewGuid().ToString(),
            data.Title,
            data.Start!.Value,
            data.End!.Value,
            data.VisibleFromLocalUserTime!.Value.FromUserTime(),
            data.RegistrationFromLocalUserTime!.Value.FromUserTime()
        );
        await eventStore.Create(e);
        return Ok();
    }

    [HttpPost("{eventId}")]
    [Authorize(Policy = "ManageEvents")]
    public async Task<IActionResult> UpdateEvent(string eventId, [FromBody] EditingEventDataDto data)
    {
        var existing = await eventStore.Get(eventId);
        if (existing == null) return NotFound();
        var errors = Validate(data);
        if (errors.Length > 0) return BadRequest(errors);
        var e = new Event(
            eventId,
            data.Title,
            data.Start!.Value,
            data.End!.Value,
            data.VisibleFromLocalUserTime!.Value.FromUserTime(),
            data.RegistrationFromLocalUserTime!.Value.FromUserTime()
        );
        await eventStore.Update(e);
        return Ok();
    }

    [HttpDelete("{eventId}")]
    [Authorize(Policy = "ManageEvents")]
    public async Task<IActionResult> DeleteEvent(string eventId)
    {
        var existing = await eventStore.Get(eventId);
        if (existing == null) return NotFound();
        if (await projectStore.HasProjects(eventId)) return Conflict("Veranstaltung enthält Projekte und kann nicht gelöscht werden.");
        await eventStore.Delete(eventId);
        return Ok();
    }

    private static string[] Validate(EditingEventDataDto data)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(data.Title))
            errors.Add("Titel darf nicht leer sein.");
        if (data.Start == null) errors.Add("Startdatum muss gesetzt werden.");
        if (data.End == null) errors.Add("Enddatum muss gesetzt werden.");
        if (data.VisibleFromLocalUserTime == null) errors.Add("Sichtbar ab muss gesetzt werden.");
        if (data.RegistrationFromLocalUserTime == null) errors.Add("Anmeldung ab muss gesetzt werden.");
        if (data.Start != null && data.End != null && data.Start.Value > data.End.Value)
            errors.Add("Startdatum muss vor dem Enddatum liegen.");
        if (data.VisibleFromLocalUserTime != null && data.RegistrationFromLocalUserTime != null && data.VisibleFromLocalUserTime.Value > data.RegistrationFromLocalUserTime.Value)
            errors.Add("Sichtbar ab muss vor Anmeldung ab liegen.");
        if (data.RegistrationFromLocalUserTime != null && data.Start != null && data.RegistrationFromLocalUserTime.Value.FromUserTime() >= data.Start.Value.ToDateTime(TimeOnly.MinValue))
            errors.Add("Anmeldung ab muss vor dem Projektzeitraum liegen.");
        return [.. errors];
    }
}
