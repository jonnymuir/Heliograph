using Microsoft.AspNetCore.DataProtection;

namespace Heliograph.Api;

/// <summary>
/// The links that let a word processor open a draft's document and save it straight back. A link carries a token that
/// names one correspondence and expires; it is signed with the host's Data Protection keys, so it cannot be made up or
/// changed, and it opens nothing else.
/// </summary>
/// <remarks>
/// A word processor cannot send the sign-in a browser has, so the token in the address is the only thing that lets it
/// in. Whoever has the link can read and replace that one document until it expires: the API hands it only to a
/// signed-in author, and it should be treated like a password.
/// </remarks>
public sealed class EditLinks(IDataProtectionProvider protection, TimeProvider clock)
{
    /// <summary>How long a link works: long enough for an editing session, short enough to stop working soon after.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    private readonly ITimeLimitedDataProtector _protector = protection.CreateProtector("Heliograph.Api.EditLinks.v1").ToTimeLimitedDataProtector();

    /// <summary>A token for one correspondence's draft document.</summary>
    public string Issue(string key) => _protector.Protect(key, clock.GetUtcNow().Add(Lifetime));

    /// <summary>The correspondence a token names, or null when it is not one this site issued, or it has expired.</summary>
    public string? Read(string token)
    {
        try
        {
            return _protector.Unprotect(token, out var expires) is var key && expires > clock.GetUtcNow() ? key : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}

/// <summary>The addresses that open a draft's document in a word processor.</summary>
/// <param name="Url">The document's WebDAV address.</param>
/// <param name="Word">Opens it for editing in Word on the desktop.</param>
/// <param name="LibreOffice">Opens it for editing in LibreOffice.</param>
/// <param name="Expires">When the link stops working.</param>
public sealed record EditLinkResponse(string Url, string Word, string LibreOffice, DateTimeOffset Expires);
