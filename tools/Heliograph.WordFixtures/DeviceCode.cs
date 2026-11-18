using System.Net.Http.Json;
using System.Text.Json;

namespace Heliograph.WordFixtures;

/// <summary>
/// Signs in to a personal Microsoft account with the device code flow: the tool shows a code, the maintainer enters it at
/// microsoft.com/devicelogin, and the tool is given a token to use their OneDrive. Nothing is stored.
/// </summary>
internal static class DeviceCode
{
    private const string Authority = "https://login.microsoftonline.com/consumers/oauth2/v2.0";
    private const string Scope = "Files.ReadWrite";

    public static async Task<string> SignInAsync(HttpClient http, string clientId)
    {
        var start = await Post(http, $"{Authority}/devicecode", new() { ["client_id"] = clientId, ["scope"] = Scope });
        Console.WriteLine(start.GetProperty("message").GetString());
        var interval = TimeSpan.FromSeconds(start.TryGetProperty("interval", out var i) ? i.GetInt32() : 5);
        var code = start.GetProperty("device_code").GetString()!;
        while (true)
        {
            await Task.Delay(interval);
            var reply = await Post(http, $"{Authority}/token", new()
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["client_id"] = clientId,
                ["device_code"] = code,
            });
            if (reply.TryGetProperty("access_token", out var token))
            {
                return token.GetString()!;
            }

            var error = reply.GetProperty("error").GetString();
            if (error is not ("authorization_pending" or "slow_down"))
            {
                throw new InvalidOperationException($"Signing in failed: {error}.");
            }
        }
    }

    private static async Task<JsonElement> Post(HttpClient http, string url, Dictionary<string, string> form)
    {
        using var response = await http.PostAsync(url, new FormUrlEncodedContent(form));
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
