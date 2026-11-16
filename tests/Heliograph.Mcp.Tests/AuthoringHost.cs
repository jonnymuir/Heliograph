using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Heliograph.Library;
using Heliograph.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Heliograph.Mcp.Tests;

/// <summary>
/// A host as a consumer would write it: its own store, its own sign-in, and one call to map the endpoint. A test
/// talks to it with a real MCP client, so it sees exactly what an agent sees.
/// </summary>
internal sealed class AuthoringHost : IAsyncDisposable
{
    public const string SignedIn = "X-Test-User";

    private readonly WebApplication _app;

    private AuthoringHost(WebApplication app, InMemoryDictionarySource dictionaries)
    {
        _app = app;
        Dictionaries = dictionaries;
    }

    public InMemoryDictionarySource Dictionaries { get; }

    public static async Task<AuthoringHost> StartAsync(string? instructions = null)
    {
        var dictionaries = new InMemoryDictionarySource();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ICorrespondenceStore, InMemoryCorrespondenceStore>();
        builder.Services.AddSingleton<IDictionarySource>(dictionaries);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderSignIn>("Test", null);
        builder.Services.AddAuthorization();
        builder.Services.AddCorrespondenceAuthoringMcp(instructions);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCorrespondenceAuthoringMcp();
        await app.StartAsync();
        return new AuthoringHost(app, dictionaries);
    }

    /// <summary>An HTTP client, signed in unless the test says otherwise.</summary>
    public HttpClient Http(bool signedIn = true)
    {
        var http = _app.GetTestClient();
        if (signedIn)
        {
            http.DefaultRequestHeaders.Add(SignedIn, "casey.contentdesigner");
        }

        return http;
    }

    public Task<McpClient> ConnectAsync() => McpClient.CreateAsync(new HttpClientTransport(
        new HttpClientTransportOptions { Endpoint = new Uri(Http().BaseAddress!, CorrespondenceAuthoringMcpExtensions.DefaultPattern) }, Http(), ownsHttpClient: true));

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    /// <summary>Signs in any request that carries the test header, as a host's real sign-in would a real user.</summary>
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

internal static class AgentCalls
{
    /// <summary>Calls a tool and returns what it sent back, as an agent would read it.</summary>
    public static async Task<JsonElement> CallAsync(this McpClient agent, string tool, object arguments)
    {
        var result = await agent.CallToolAsync(tool, Arguments(arguments));
        var text = Text(result);
        result.IsError.Should().NotBe(true, text);
        return JsonDocument.Parse(text).RootElement;
    }

    /// <summary>Calls a tool that is expected to refuse, and returns what it told the agent.</summary>
    public static async Task<string> RefusalAsync(this McpClient agent, string tool, object arguments)
    {
        var result = await agent.CallToolAsync(tool, Arguments(arguments));
        result.IsError.Should().BeTrue();
        return Text(result);
    }

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    private static Dictionary<string, object?> Arguments(object arguments) =>
        JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(arguments))!;
}
