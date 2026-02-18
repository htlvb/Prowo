using Microsoft.Identity.Web;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Shared;
using System.Reflection;

public static class ServerConfigurationExtensions
{
    public static IServiceCollection AddProwoAuthorizationRules(this IServiceCollection services)
    {
        return services.AddAuthorization(options =>
        {
            // By default, all incoming requests will be authorized according to the default policy
            //options.FallbackPolicy = options.DefaultPolicy;

            options.AddPolicy("CreateProject", policy =>
            {
                policy.RequireAssertion(ctx =>
                {
                    if (ctx.User.IsInRole("all-projects-editor"))
                    {
                        return true;
                    }
                    if (ctx.User.IsInRole("project-creator"))
                    {
                        if (ctx.Resource == null)
                        {
                            return true;
                        }
                        if (ctx.Resource is Project p && p.Organizer.Id == ctx.User.GetObjectId())
                        {
                            return true;
                        }
                    }
                    return false;
                });
            });
            options.AddPolicy("UpdateProject", policy =>
            {
                policy.RequireAssertion(ctx =>
                {
                    if (ctx.User.IsInRole("all-projects-editor"))
                    {
                        return true;
                    }
                    if (ctx.User.IsInRole("project-creator") && ctx.Resource is Project p && p.Organizer.Id == ctx.User.GetObjectId())
                    {
                        return true;
                    }
                    return false;
                });
            });

            options.AddPolicy("DeleteProject", policy =>
            {
                policy.RequireAssertion(ctx =>
                {
                    if (ctx.User.IsInRole("all-projects-editor"))
                    {
                        return true;
                    }
                    if (ctx.User.IsInRole("project-creator") && ctx.Resource is Project p && p.Organizer.Id == ctx.User.GetObjectId() && p.AllAttendees.Count == 0)
                    {
                        return true;
                    }
                    return false;
                });
            });

            options.AddPolicy("ChangeProjectOrganizer", policy => policy.RequireRole("all-projects-editor"));
            options.AddPolicy("AttendProject", policy => policy.RequireRole("project-attendee"));
            options.AddPolicy("CreateReport", policy => policy.RequireRole("report-viewer"));
        });
    }

    public static IMvcBuilder AddProwoControllers(this IServiceCollection services)
    {
        return services
            .AddControllersWithViews()
            .AddApplicationPart(Assembly.GetExecutingAssembly()) // for tests
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
            });
    }
}
