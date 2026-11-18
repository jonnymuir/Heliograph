using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Heliograph.WordFixtures;

/// <summary>
/// Has Word make a PDF of a document: uploads it to a folder of its own in the maintainer's OneDrive, downloads it again as
/// PDF (Microsoft Graph's format=pdf, laid out by Word), and deletes the upload.
/// </summary>
internal sealed class WordConverter(HttpClient http, string token)
{
    private const string Graph = "https://graph.microsoft.com/v1.0/me/drive";
    private const string Folder = "Heliograph Word fixtures";

    public async Task<byte[]> ConvertAsync(string name, byte[] docx)
    {
        using var upload = Request(HttpMethod.Put, $"{Graph}/root:/{Uri.EscapeDataString(Folder)}/{Uri.EscapeDataString(name)}.docx:/content");
        upload.Content = new ByteArrayContent(docx);
        upload.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        using var uploaded = await Send(upload);
        var id = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        try
        {
            using var download = Request(HttpMethod.Get, $"{Graph}/items/{id}/content?format=pdf");
            using var pdf = await Send(download);
            return await pdf.Content.ReadAsByteArrayAsync();
        }
        finally
        {
            using var delete = Request(HttpMethod.Delete, $"{Graph}/items/{id}");
            using var _ = await http.SendAsync(delete);
        }
    }

    private HttpRequestMessage Request(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<HttpResponseMessage> Send(HttpRequestMessage request)
    {
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            response.Dispose();
            throw new InvalidOperationException($"{request.Method} {request.RequestUri?.AbsolutePath} answered {(int)response.StatusCode}: {body}");
        }

        return response;
    }
}
