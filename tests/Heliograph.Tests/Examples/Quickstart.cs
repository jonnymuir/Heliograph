using System.Globalization;
using System.Text.Json;
using Heliograph.Documents;

namespace Heliograph.Tests.Examples;

/// <summary>
/// The code the README shows. It lives here so that it is compiled and run: a test fails if the README and this
/// file differ, and another runs it against a benchmark letter.
/// </summary>
public static class Quickstart
{
    public static void Run(string dictionaryPath, string templatePath, string recipientPath, string letterPath)
    {
        // <readme>
        var dictionary = FieldDictionary.Parse(File.ReadAllText(dictionaryPath));

        // Compile once. A template that does not compile comes back with every problem found.
        using var template = File.OpenRead(templatePath);
        var compilation = DocumentTemplate.Compile(template, dictionary);
        foreach (var problem in compilation.Diagnostics)
        {
            Console.WriteLine($"{problem.Severity} {problem.Code}: {problem.Message} {problem.Fix}");
        }

        if (compilation.Document is not { } document)
        {
            return;
        }

        // Render as often as you like, from any thread: once for each recipient.
        var recipient = JsonDocument.Parse(File.ReadAllText(recipientPath)).RootElement;
        var render = document.Render(recipient, new RenderSettings(CultureInfo.GetCultureInfo("en-GB")));
        if (render.Docx is { } letter)
        {
            File.WriteAllBytes(letterPath, letter);
        }

        // </readme>
    }
}
