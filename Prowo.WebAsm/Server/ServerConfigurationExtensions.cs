﻿using Microsoft.Identity.Web;
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
                    if (ctx.User.IsInRole("Project.Write.All"))
                    {
                        return true;
                    }
                    if (ctx.User.IsInRole("Project.Write"))
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
                    if (ctx.User.IsInRole("Project.Write.All"))
                    {
                        return true;
                    }
                    if (ctx.User.IsInRole("Project.Write") && ctx.Resource is Project p && p.Organizer.Id == ctx.User.GetObjectId())
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
                    if (ctx.User.IsInRole("Project.Write.All"))
                    {
                        return true;
                    }
                    if (ctx.User.IsInRole("Project.Write") && ctx.Resource is Project p && p.Organizer.Id == ctx.User.GetObjectId() && p.AllAttendees.Count == 0)
                    {
                        return true;
                    }
                    return false;
                });
            });

            options.AddPolicy("ChangeProjectOrganizer", policy => policy.RequireRole("Project.Write.All"));
            options.AddPolicy("AttendProject", policy => policy.RequireRole("Project.Attend"));
            options.AddPolicy("CreateReport", policy => policy.RequireRole("Report.Create"));
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
