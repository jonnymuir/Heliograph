using System.Security;
using System.Text;
using Heliograph.Pdf.Layout;
using Heliograph.Pdf.Reading;
using static Heliograph.Pdf.Writing.PdfObjects;

namespace Heliograph.Pdf.Writing;

/// <summary>
/// Writes laid-out pages as a PDF meant to meet PDF/UA-1: tagged, every piece of text in the structure or an artifact,
/// fonts embedded, the language and title declared and the title shown. Nothing depends on the clock.
/// </summary>
internal static class DocumentWriter
{
    public static byte[] Write(WordDocument document, IReadOnlyList<LaidOutPage> pages, StructNode root)
    {
        var objects = new PdfObjects();
        var (catalog, pagesId, structRoot, info) = (objects.Reserve(), objects.Reserve(), objects.Reserve(), objects.Reserve());
        var fonts = new FontEmbedder(objects);
        var pageIds = pages.Select(_ => objects.Reserve()).ToList();
        var marked = new List<List<StructNode>>();
        var g = document.Section.Page;
        for (var n = 0; n < pages.Count; n++)
        {
            var writer = new ContentWriter(fonts, g.Height);
            var content = objects.Stream(writer.Write(n, pages[n]));
            var fontDict = string.Join(" ", writer.FontsUsed.Select(f => Invariant($"/{f.Name} {f.Id} 0 R")));
            objects.Set(pageIds[n], Invariant($"<</Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 {N(g.Width)} {N(g.Height)}] ") +
                Invariant($"/Resources <</Font <<{fontDict}>>>> /Contents {content} 0 R /StructParents {n} /Tabs /S>>"));
            marked.Add(writer.Marked);
        }

        fonts.WriteAll();
        StructureWriter.Write(objects, structRoot, root, pageIds, marked);
        objects.Set(pagesId, Invariant($"<</Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => Invariant($"{id} 0 R")))}] /Count {pages.Count}>>"));

        // PDF/UA needs a title. A document without one was warned about at compile (HG1020); its PDF says what it is.
        var title = document.Title.Length > 0 ? document.Title : "Letter";
        var metadata = objects.Stream(Encoding.UTF8.GetBytes(Xmp(title)), " /Type /Metadata /Subtype /XML", compress: false);
        objects.Set(info, $"<</Title {Utf16(title)} /Producer (Heliograph)>>");
        objects.Set(catalog, Invariant($"<</Type /Catalog /Pages {pagesId} 0 R /StructTreeRoot {structRoot} 0 R /MarkInfo <</Marked true>> ") +
            Invariant($"/Lang {Utf16(document.Language)} /Metadata {metadata} 0 R /ViewerPreferences <</DisplayDocTitle true>>>>"));
        return objects.Assemble(catalog, info);
    }

    private static string Xmp(string title) => $"""
        <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?>
        <x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
        <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:pdfuaid="http://www.aiim.org/pdfua/ns/id/">
        <dc:title><rdf:Alt><rdf:li xml:lang="x-default">{SecurityElement.Escape(title)}</rdf:li></rdf:Alt></dc:title>
        <pdfuaid:part>1</pdfuaid:part>
        </rdf:Description></rdf:RDF></x:xmpmeta>
        <?xpacket end="w"?>
        """;
}
