using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using Duende.IdentityModel.OidcClient.Browser;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// see https://github.com/DuendeSoftware/foss/blob/main/identity-model-oidc-client/samples/NetCoreConsoleClient/src/NetCoreConsoleClient/SystemBrowser.cs
public class SystemBrowser(LoopbackHttpListener loopbackHttpListener) : IBrowser
{
    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        OpenBrowser(options.StartUrl);

        try
        {
            var result = await loopbackHttpListener.Request;
            return new BrowserResult { Response = result, ResultType = BrowserResultType.Success };
        }
        catch (Exception ex)
        {
            return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = ex.Message };
        }
    }

    private static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
    }
}

public class LoopbackHttpListener(WebApplication app, string serverUrl, TaskCompletionSource<string> request) : IAsyncDisposable
{
    public Task<string> Request => request.Task;

    public string ServerUrl { get; } = serverUrl;

    public async ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
    }

    public static async Task<LoopbackHttpListener> Start(int? port = default)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        TaskCompletionSource<string> request = new();
        app.Use(async (HttpContext ctx, Func<Task> next) =>
        {
            if (ctx.Request.Method != "GET")
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
            else if (ctx.Request.QueryString.Value == null)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            else
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync("<h1>Successfully signed in</h1><p>You can now return to the application.</p>");
                await ctx.Response.Body.FlushAsync();

                request.TrySetResult(ctx.Request.QueryString.Value);
            }
        });
        var url = new TaskCompletionSource<string>();
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var server = app.Services.GetRequiredService<IServer>();
            var serverAddress = server.Features.GetRequiredFeature<IServerAddressesFeature>();
            url.SetResult(serverAddress.Addresses.First());
        });
        _ = app.RunAsync($"https://127.0.0.1:{port ?? 0}");
        var serverUrl = (await url.Task).Replace("127.0.0.1", "localhost");
        return new LoopbackHttpListener(app, serverUrl, request);
    }
}