using Heliograph.WordFixtures;

// Converts each .docx in the Word fixtures' inputs folder to PDF with Microsoft Word, through Microsoft Graph and the
// maintainer's own OneDrive, and saves the PDF beside it. Never shipped; run by hand. Only the fictional fixtures go up.
var root = args.Length > 0 ? args[0] : Path.Combine(Repository.Root(), "tests", "Heliograph.Tests", "Fixtures", "Word");
var inputs = Path.Combine(root, "inputs");
var clientId = Environment.GetEnvironmentVariable("HELIOGRAPH_WORD_CLIENT_ID") ?? Settings.ClientId;
if (string.IsNullOrEmpty(clientId))
{
    Console.Error.WriteLine("Set HELIOGRAPH_WORD_CLIENT_ID to the app registration's client ID (see tools/Heliograph.WordFixtures/README.md).");
    return 1;
}

using var http = new HttpClient();
var token = await DeviceCode.SignInAsync(http, clientId);
var word = new WordConverter(http, token);
foreach (var docx in Directory.GetFiles(inputs, "*.docx").Order(StringComparer.Ordinal))
{
    var name = Path.GetFileNameWithoutExtension(docx);
    Console.Write($"{name} ... ");
    var pdf = await word.ConvertAsync(name, await File.ReadAllBytesAsync(docx));
    await File.WriteAllBytesAsync(Path.Combine(root, $"{name}.word.pdf"), pdf);
    Console.WriteLine($"{pdf.Length / 1024} KB");
}

return 0;
