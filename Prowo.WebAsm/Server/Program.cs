using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Azure.Cosmos;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;
using Npgsql;
using Prowo.WebAsm.Server.Data;
using Prowo.WebAsm.Shared;
using System.Globalization;
using GraphServiceClient = Microsoft.Graph.GraphServiceClient;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-AT");

NpgsqlConnection.GlobalTypeMapper.UseJsonNet();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLocalization();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(builder.Configuration.GetSection("GraphBeta"))
    .AddInMemoryTokenCaches();
builder.Services.AddControllersWithViews().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
    options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
});
builder.Services.AddRazorPages();

builder.Services.AddAuthorization(options =>
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

    options.AddPolicy("ChangeProjectOrganizer", policy => policy.RequireRole("Project.Write.All"));
    options.AddPolicy("AttendProject", policy => policy.RequireRole("Project.Attend"));
    options.AddPolicy("CreateReport", policy => policy.RequireRole("Report.Create"));
});

builder.Services.AddSingleton(provider =>
{
    string connectionString = builder.Configuration.GetConnectionString("PostgresqlDb");
    return new PostgresqlProjectStore(connectionString);
});

builder.Services.AddSingleton(provider =>
{
    string connectionString = builder.Configuration.GetConnectionString("CosmosDb");
    return new ProjectStore(new CosmosClient(connectionString));
});

builder.Services.AddScoped(provider =>
{
    return new UserStore(
        builder.Configuration.GetSection("AppSettings")["OrganizerGroupId"],
        builder.Configuration.GetSection("AppSettings")["AttendeeGroupId"],
        provider.GetRequiredService<GraphServiceClient>());
});

var app = builder.Build();

app.UseForwardedHeaders(new() { ForwardedHeaders = ForwardedHeaders.XForwardedProto });

app.UseRequestLocalization(CultureInfo.CurrentCulture.Name);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    IdentityModelEventSource.ShowPII = true;
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles(new StaticFileOptions { ServeUnknownFileTypes = true }); // support let's encrypt challenge

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
