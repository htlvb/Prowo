using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Shared;

namespace Prowo.WebAsm.Server;

public static class DtoMappingExtensions
{
    public static UserRoleForProjectDto ToDto(this UserRoleForProject userRole)
    {
        return userRole switch
        {
            UserRoleForProject.NotRelated => UserRoleForProjectDto.NotRelated,
            UserRoleForProject.Registered => UserRoleForProjectDto.Registered,
            UserRoleForProject.Waiting => UserRoleForProjectDto.Waiting,
            UserRoleForProject.Organizer => UserRoleForProjectDto.Organizer,
            UserRoleForProject.CoOrganizer => UserRoleForProjectDto.CoOrganizer,
            _ => throw new ArgumentOutOfRangeException(nameof(userRole), userRole, "Can't map user role to DTO")
        };
    }
    
    public static ProjectOrganizerDto ToDto(this ProjectOrganizer organizer)
    {
        return new ProjectOrganizerDto(organizer.Id, $"{organizer.LastName} {organizer.FirstName} ({organizer.ShortName})");
    }
}
