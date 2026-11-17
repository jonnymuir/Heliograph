using System.Security.Claims;
using System.Text.Encodings.Web;
using Heliograph.Library;
using Heliograph.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heliograph.Api.Tests;

/// <summary>A host as a consumer writes it: its own store and sign-in, and one call to map the API.</summary>
internal sealed class ApiHost : IAsyncDisposable
{
    private const string SignedIn = "X-Test-User";
    private readonly WebApplication _app;

    private ApiHost(WebApplication app, InMemoryDictionarySource dictionaries)
    {
        _app = app;
        Dictionaries = dictionaries;
    }

    public InMemoryDictionarySource Dictionaries { get; }

    public static async Task<ApiHost> StartAsync()
    {
        var dictionaries = new InMemoryDictionarySource();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ICorrespondenceStore, InMemoryCorrespondenceStore>();
        builder.Services.AddSingleton<IDictionarySource>(dictionaries);
        builder.Services.AddCorrespondenceLibrary();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderSignIn>("Test", null);
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCorrespondenceAuthoringApi();
        await app.StartAsync();
        return new ApiHost(app, dictionaries);
    }

    public HttpClient Http(bool signedIn = true)
    {
        var http = _app.GetTestClient();
        http.BaseAddress = new Uri(http.BaseAddress!, CorrespondenceAuthoringApiExtensions.DefaultPrefix + "/");
        if (signedIn)
        {
            http.DefaultRequestHeaders.Add(SignedIn, "casey.contentdesigner");
        }

        return http;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private sealed class HeaderSignIn(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            Request.Headers.TryGetValue(SignedIn, out var user)
                ? AuthenticateResult.Success(new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, user.ToString())], Scheme.Name)), Scheme.Name))
                : AuthenticateResult.NoResult());
    }
}
