using System.Globalization;
using System.Text.Json;

namespace Heliograph.Tests.Examples;

/// <summary>
/// The code the README shows. It lives here so that it is compiled and run: a test fails if the README and this
/// file differ, and another runs it against a benchmark letter.
/// </summary>
public static class Quickstart
{
    public static void Run(string folder, string recipientPath, string output)
    {
        // <readme>
        var dictionary = FieldDictionary.Parse(File.ReadAllText(Path.Combine(folder, "dictionary.json")));

        // A correspondence has a part for each channel it is sent on. Leave out any you do not use.
        using var document = File.OpenRead(Path.Combine(folder, "document.docx"));
        var compilation = Correspondence.Compile(new CorrespondenceSource
        {
            Document = document,
            Email = File.ReadAllText(Path.Combine(folder, "email.md")),
            Sms = File.ReadAllText(Path.Combine(folder, "sms.txt")),
        }, dictionary);

        // Compile once. Every problem in every part comes back together, with where it is and how to fix it.
        foreach (var problem in compilation.Diagnostics)
        {
            Console.WriteLine($"{problem.Severity} {problem.Code}: {problem.Message} {problem.Fix}");
        }

        if (compilation.Correspondence is not { } correspondence)
        {
            return;
        }

        // Render as often as you like, from any thread: once for each recipient.
        var recipient = JsonDocument.Parse(File.ReadAllText(recipientPath)).RootElement;
        var rendered = correspondence.Render(recipient, new RenderSettings(CultureInfo.GetCultureInfo("en-GB")));
        if (rendered.Diagnostics.Count > 0)
        {
            return; // The data was wrong, so nothing is produced. The diagnostics say what to supply.
        }

        File.WriteAllBytes(Path.Combine(output, "letter.docx"), rendered.Docx!);
        File.WriteAllText(Path.Combine(output, "email.html"), rendered.Email!.Html);
        File.WriteAllText(Path.Combine(output, "sms.txt"), rendered.Sms!.Text);

        // </readme>
    }
}
