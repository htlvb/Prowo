using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Graph;
using Microsoft.Azure.Cosmos;
using Prowo.Web.Data;
using System.Globalization;
using Microsoft.AspNetCore.HttpOverrides;

namespace Prowo.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLocalization();

            var initialScopes = Configuration.GetValue<string>("DownstreamApi:Scopes")?.Split(' ');

            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAd"))
                    .EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
                        .AddMicrosoftGraph(Configuration.GetSection("DownstreamApi"))
                        .AddInMemoryTokenCaches();
            services.AddControllersWithViews()
                .AddMicrosoftIdentityUI();

            services.AddAuthorization(options =>
            {
                // By default, all incoming requests will be authorized according to the default policy
                options.FallbackPolicy = options.DefaultPolicy;

                options.AddPolicy("EditProject", policy =>
                {
                    policy.RequireAssertion(context =>
                    {
                        if (context.User.IsInRole("Project.Write.All"))
                        {
                            return true;
                        }
                        if (context.Resource is Pages.ListProjects.UIProject lp)
                        {
                            return lp.OrganizerId == context.User.GetObjectId();
                        }
                        if (context.Resource is Pages.EditProject ep)
                        {
                            if (ep.EditingProject.OrganizerId == context.User.GetObjectId())
                            {
                                return true;
                            }
                            if (context.User.IsInRole("Project.Write") && ep.ProjectId == null)
                            {
                                return true;
                            }
                            return false;
                        }
                        return false;
                    });
                });
            });

            services.AddRazorPages().AddMvcOptions(options => {}).AddMicrosoftIdentityUI();
            services.AddServerSideBlazor()
                .AddMicrosoftIdentityConsentHandler();

            services.AddSingleton(provider =>
            {
                string connectionString = Configuration.GetConnectionString("CosmosDb");
                return new ProjectStore(new CosmosClient(connectionString));
            });

            services.AddScoped(provider =>
            {
                return new UserStore(
                    Configuration.GetSection("AppSettings")["OrganizerGroupId"],
                    Configuration.GetSection("AppSettings")["AttendeeGroupId"],
                    provider.GetRequiredService<GraphServiceClient>(),
                    provider.GetRequiredService<MicrosoftIdentityConsentAndConditionalAccessHandler>());
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders(new() { ForwardedHeaders = ForwardedHeaders.XForwardedProto });

            app.UseRequestLocalization(CultureInfo.CurrentCulture.Name);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
