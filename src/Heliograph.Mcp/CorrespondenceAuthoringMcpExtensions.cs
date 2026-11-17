using Heliograph.Documents;
using Heliograph.Library;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Heliograph.Mcp;

/// <summary>
/// Registers and maps correspondence authoring as MCP tools over HTTP, in the same process as the host's own
/// screens, so a tool call reaches the same drafts a person sees.
/// </summary>
public static class CorrespondenceAuthoringMcpExtensions
{
    /// <summary>The address the endpoint is mapped at unless the host chooses another.</summary>
    public const string DefaultPattern = "/heliograph/correspondence-authoring/mcp";

    /// <summary>
    /// Registers the MCP server with the authoring tools, the prompts, and every guide in docs/skills as a resource.
    /// The host registers its own <see cref="ICorrespondenceStore"/> and <see cref="IDictionarySource"/>.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="instructions">
    /// Anything about this host that an agent should know when it connects, added after
    /// <see cref="CorrespondenceAuthoringInstructions.Default"/>: for example which dictionary belongs to which
    /// service. Leave null when there is nothing to add.
    /// </param>
    public static IServiceCollection AddCorrespondenceAuthoringMcp(this IServiceCollection services, string? instructions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(provider => new CorrespondenceLibrary(
            provider.GetRequiredService<ICorrespondenceStore>(),
            provider.GetRequiredService<IDictionarySource>(),
            provider.GetService<TimeProvider>() ?? TimeProvider.System,
            provider.GetService<IDictionaryStartingPoints>(),
            DocumentChecks(provider)));

        services.AddMcpServer(options => options.ServerInstructions = CorrespondenceAuthoringInstructions.Compose(instructions))
            .WithHttpTransport()
            .WithToolsFromAssembly(typeof(CorrespondenceAuthoringTools).Assembly)
            .WithPromptsFromAssembly(typeof(CorrespondenceAuthoringPrompts).Assembly)
            .WithResources(Skills.Resources());

        return services;
    }

    /// <summary>
    /// Maps the MCP endpoint. Deny by default: it requires a signed-in caller, or the named
    /// <paramref name="authorizationPolicy"/>. Its tools can read, overwrite and publish every correspondence, and a
    /// preview shows whatever data it is given, so a host must write <c>allowAnonymous: true</c> to open it.
    /// Returns the endpoint so the host can add a stricter policy.
    /// </summary>
    public static IEndpointConventionBuilder MapCorrespondenceAuthoringMcp(
        this IEndpointRouteBuilder endpoints,
        string pattern = DefaultPattern,
        string? authorizationPolicy = null,
        bool allowAnonymous = false)
    {
        if (allowAnonymous && authorizationPolicy is not null)
        {
            throw new ArgumentException("authorizationPolicy and allowAnonymous: true contradict each other.", nameof(authorizationPolicy));
        }

        var endpoint = endpoints.MapMcp(pattern);
        if (allowAnonymous)
        {
            endpoint.AllowAnonymous();
        }
        else if (authorizationPolicy is null)
        {
            endpoint.RequireAuthorization();
        }
        else
        {
            endpoint.RequireAuthorization(authorizationPolicy);
        }

        return endpoint;
    }

    // A PDF converter that knows what it cannot lay out says so when a template is compiled: registering it is enough.
    private static List<IDocumentCheck> DocumentChecks(IServiceProvider provider) =>
        [.. provider.GetServices<IDocumentCheck>().Concat(provider.GetService<IPdfConverter>() is IDocumentCheck check ? [check] : []).Distinct()];
}
