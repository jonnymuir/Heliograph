using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Heliograph.ReferenceApp.Tests;

/// <summary>Starts the reference app on an empty folder: what someone who presses Run gets.</summary>
public sealed class ReferenceAppTests : IClassFixture<ReferenceAppTests.Site>
{
    private readonly HttpClient _http;

    public ReferenceAppTests(Site site) => _http = site.CreateClient();

    [Fact]
    public async Task It_starts_with_the_three_benchmark_letters_published()
    {
        var list = await _http.GetFromJsonAsync<JsonElement>("/heliograph/api/correspondence");

        list.EnumerateArray().Select(c => (c.GetProperty("key").GetString(), c.GetProperty("latestVersion").GetInt32()))
            .Should().Equal(("acknowledgement", 1), ("annual-statement", 1), ("decision-letter", 1));
    }

    [Fact]
    public async Task The_workbench_page_and_its_script_are_served()
    {
        var page = await _http.GetStringAsync("/heliograph");

        page.Should().Contain("<heliograph-workbench api=\"/heliograph/api\">");
        (await _http.GetAsync("/_content/Heliograph.Workbench/heliograph-workbench.js")).StatusCode
            .Should().Be(HttpStatusCode.OK, "the workbench client must be built first: see the reference app guide");
    }

    [Fact]
    public async Task Every_benchmark_letter_previews_with_each_of_its_samples()
    {
        foreach (var key in new[] { "acknowledgement", "decision-letter", "annual-statement" })
        {
            var samples = await _http.GetFromJsonAsync<string[]>($"/heliograph/api/dictionaries/{key}/samples");
            samples.Should().NotBeEmpty();
            foreach (var sample in samples!)
            {
                var preview = await (await _http.PostAsJsonAsync($"/heliograph/api/correspondence/{key}/preview", new { sample })).Content.ReadFromJsonAsync<JsonElement>();
                preview.GetProperty("diagnostics").GetArrayLength().Should().Be(0, $"{key} with {sample}");
            }
        }
    }

    [Fact]
    public async Task The_outbox_shows_what_a_host_would_send_for_a_published_version()
    {
        var page = await _http.GetStringAsync("/outbox/annual-statement/typical");

        page.Should().Contain("Version <strong>1</strong> was rendered");
    }

    [Fact]
    public async Task An_agent_can_reach_the_MCP_endpoint()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/heliograph/correspondence-authoring/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "test", version = "1" } } }),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        (await _http.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The reference app on a folder of its own, removed afterwards.</summary>
    public sealed class Site : WebApplicationFactory<Program>
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "heliograph-reference-" + Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Heliograph:Folder"] = _folder }));

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
    }
}
