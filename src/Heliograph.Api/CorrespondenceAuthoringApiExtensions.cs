using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Heliograph.Api;

/// <summary>
/// Maps the authoring operations as REST endpoints over <see cref="Library.CorrespondenceLibrary"/>: the same
/// operations the MCP server offers, so a person at the workbench and an agent get the same answers. Deny by
/// default: every route needs a signed-in caller, or the named policy.
/// </summary>
public static class CorrespondenceAuthoringApiExtensions
{
    /// <summary>The address the endpoints sit under unless the host chooses another.</summary>
    public const string DefaultPrefix = "/heliograph/api";

    /// <param name="endpoints">The host's routes.</param>
    /// <param name="prefix">Where the endpoints sit.</param>
    /// <param name="authorizationPolicy">
    /// The policy every route requires. Null means any signed-in caller. Without authentication registered, the
    /// routes fail closed.
    /// </param>
    /// <param name="allowAnonymous">
    /// Opens every route to anyone. Only for a local demo with made-up data: these routes can overwrite and publish
    /// every correspondence, and render takes recipients' personal data. Write the reason beside the call.
    /// </param>
    public static RouteGroupBuilder MapCorrespondenceAuthoringApi(
        this IEndpointRouteBuilder endpoints,
        string prefix = DefaultPrefix,
        string? authorizationPolicy = null,
        bool allowAnonymous = false)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (allowAnonymous && authorizationPolicy is not null)
        {
            throw new ArgumentException("authorizationPolicy and allowAnonymous: true contradict each other.", nameof(authorizationPolicy));
        }

        var group = endpoints.MapGroup(prefix);
        if (allowAnonymous)
        {
            group.AllowAnonymous();
        }
        else if (authorizationPolicy is null)
        {
            group.RequireAuthorization();
        }
        else
        {
            group.RequireAuthorization(authorizationPolicy);
        }

        DictionaryEndpoints.Map(group);
        DictionaryEditingEndpoints.Map(group);
        DraftEndpoints.Map(group);
        DocumentEndpoints.Map(group);
        RenderEndpoints.Map(group);
        WebDavEndpoints.Map(endpoints);
        return group;
    }
}
