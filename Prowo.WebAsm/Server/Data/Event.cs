namespace Prowo.WebAsm.Server.Data;

public record Event(
    string Id,
    string Title,
    DateOnly Start,
    DateOnly End,
    DateTime VisibleFrom,
    DateTime RegistrationFrom
);
