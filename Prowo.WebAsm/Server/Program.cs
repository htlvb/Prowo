using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;
using Prowo.WebAsm.Server.Data;
using System.Globalization;
using GraphServiceClient = Microsoft.Graph.GraphServiceClient;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-AT");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLocalization();

builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration)
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(builder.Configuration.GetSection("Graph"))
    .AddInMemoryTokenCaches();
builder.Services.AddProwoControllers();
builder.Services.AddRazorPages();

builder.Services.AddProwoAuthorizationRules();

builder.Services.AddSingleton<IProjectStore>(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("Pgsql") ?? throw new Exception("\"ConnectionStrings:Pgsql\" not found");
    return new PgsqlProjectStore(connectionString);
});

builder.Services.AddScoped<IUserStore>(provider =>
{
    return new UserStore(
        builder.Configuration.GetSection("AppSettings")["OrganizerGroupId"] ?? throw new Exception("\"AppSettings:OrganizerGroupId\" not found"),
        builder.Configuration.GetSection("AppSettings")["AttendeeGroupId"] ?? throw new Exception("\"AppSettings:OrganizerGroupId\" not found"),
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
