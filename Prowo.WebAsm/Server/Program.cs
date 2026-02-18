using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Logging;
using Prowo.WebAsm.Server.Data;
using System.Globalization;
using System.Text.RegularExpressions;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-AT");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLocalization();

builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        builder.Configuration.GetSection("Oidc").Bind(options);
    });
builder.Services.AddTransient<IClaimsTransformation, KeycloakRolesClaimsTransformation>();
builder.Services.AddProwoControllers();
builder.Services.AddRazorPages();

builder.Services.AddProwoAuthorizationRules();

builder.Services.AddSingleton<TimeProvider, LocalTimeProvider>();

builder.Services.AddSingleton<IProjectStore>(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("Pgsql") ?? throw new Exception("\"ConnectionStrings:Pgsql\" not found");
    var timeProvider = provider.GetRequiredService<TimeProvider>();
    return new PgsqlProjectStore(connectionString, timeProvider);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(provider =>
{
    return new KeycloakAdminApiClientFactory(
        builder.Configuration.GetSection("Keycloak")["BaseUrl"] ?? throw new Exception("\"Keycloak:BaseUrl\" not found"),
        provider.GetRequiredService<IHttpContextAccessor>()
    );
});
builder.Services.AddScoped<IUserStore>(provider =>
{
    return new KeycloakUserStore(
        builder.Configuration.GetSection("Keycloak")["RealmName"] ?? throw new Exception("\"Keycloak:RealmName\" not found"),
        builder.Configuration.GetSection("UserStore")["OrganizerGroupId"] ?? throw new Exception("\"UserStore:OrganizerGroupId\" not found"),
        builder.Configuration.GetSection("UserStore")["AttendeeGroupId"] ?? throw new Exception("\"UserStore:OrganizerGroupId\" not found"),
        new Regex(builder.Configuration.GetSection("UserStore")["IncludedClasses"] ?? ""),
        provider.GetRequiredService<KeycloakAdminApiClientFactory>(),
        provider.GetRequiredService<IHttpContextAccessor>());
});

builder.Services.AddSingleton<IRegistrationStrategy>(provider => new LogicalAndCombinationStrategy([
    new NoRegistrationAfterClosingDateStrategy(provider.GetRequiredService<TimeProvider>()),
    new NoRegistrationIfRegisteredStrategy(),
    new NoWaitingListStrategy(),
    new SingleRegistrationPerDayStrategy()
]));

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
