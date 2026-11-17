using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Heliograph.PdfExperiment.OwnEngine;

/// <summary>
/// Writes laid-out pages as a tagged PDF meant to meet PDF/UA-1: fonts embedded as subsets with a Unicode map, every
/// piece of text either in the structure tree or marked as an artifact, the language and title declared. Nothing in it
/// depends on the clock, so the same letter always gives the same bytes.
/// </summary>
public sealed class PdfWriter(Fonts fonts)
{
    private readonly List<byte[]> _objects = [];
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public byte[] Write(WordDocument document, IReadOnlyList<LaidOutPage> pages, StructNode root)
    {
        var catalog = Reserve();
        var pagesId = Reserve();
        var structRoot = Reserve();
        var metadata = Reserve();
        var info = Reserve();

        // One font resource per face used, each a subset holding only its glyphs.
        var faces = new Dictionary<TrueType, (string Name, int Id, Dictionary<ushort, string> Glyphs)>();
        (string Name, int Id, Dictionary<ushort, string> Glyphs) Font(RunStyle style)
        {
            var face = fonts.Face(style);
            if (!faces.TryGetValue(face, out var entry)) faces[face] = entry = ($"F{faces.Count + 1}", Reserve(), []);
            return entry;
        }

        var pageIds = pages.Select(_ => Reserve()).ToList();
        var parentTree = new List<List<StructNode>>();
        var height = document.Section.Height;
        for (var n = 0; n < pages.Count; n++)
        {
            var content = new StringBuilder();
            var mcids = new List<StructNode>();
            var usedFonts = new HashSet<string>();
            StructNode? open = null;
            var inArtifact = false;
            void Close()
            {
                if (open is not null || inArtifact) content.Append("EMC\n");
                open = null;
                inArtifact = false;
            }

            foreach (var op in pages[n].Ops)
            {
                if (op is TextOp text)
                {
                    if (text.Node is null)
                    {
                        if (!inArtifact) { Close(); content.Append("/Artifact <</Type /Pagination>> BDC\n"); inArtifact = true; }
                    }
                    else if (open != text.Node || inArtifact)
                    {
                        Close();
                        text.Node.Kids.Add(new MarkedRef(n, mcids.Count));
                        content.Append($"/{text.Node.Type} <</MCID {mcids.Count}>> BDC\n");
                        mcids.Add(text.Node);
                        open = text.Node;
                    }

                    var (name, _, glyphs) = Font(text.Style);
                    usedFonts.Add(name);
                    var face = fonts.Face(text.Style);
                    var hex = new StringBuilder();
                    foreach (var rune in text.Text.EnumerateRunes())
                    {
                        var glyph = face.Glyph(rune.Value);
                        glyphs.TryAdd(glyph, rune.ToString());
                        hex.Append(glyph.ToString("X4"));
                    }

                    content.Append(Invariant, $"BT /{name} {N(text.Style.Size)} Tf 1 0 0 1 {N(text.X)} {N(height - text.Baseline)} Tm <{hex}> Tj ET\n");
                    if (text.Style.Underline)
                    {
                        var w = face.Width(text.Text, text.Style.Size);
                        content.Append(Invariant, $"{N(text.Style.Size / 20)} w {N(text.X)} {N(height - text.Baseline - 1.5)} m {N(text.X + w)} {N(height - text.Baseline - 1.5)} l S\n");
                    }
                }
                else if (op is LineOp line)
                {
                    if (!inArtifact) { Close(); content.Append("/Artifact BMC\n"); inArtifact = true; }
                    content.Append(Invariant, $"{N(line.Width)} w 0 J {N(line.X1)} {N(height - line.Y1)} m {N(line.X2)} {N(height - line.Y2)} l S\n");
                }
            }

            Close();
            var stream = Stream(Encoding.ASCII.GetBytes(content.ToString()));
            var fontDict = string.Join(" ", faces.Values.Where(f => usedFonts.Contains(f.Name)).Select(f => $"/{f.Name} {f.Id} 0 R"));
            Set(pageIds[n], $"<</Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 {N(document.Section.Width)} {N(height)}] " +
                $"/Resources <</Font <<{fontDict}>>>> /Contents {stream} 0 R /StructParents {n} /Tabs /S>>");
            parentTree.Add(mcids);
        }

        foreach (var (face, (name, id, glyphs)) in faces) WriteFont(face, id, name, glyphs);

        // The structure tree: each element knows its parent, its kids, and the page each marked piece is on.
        var ids = new Dictionary<StructNode, int>();
        void Assign(StructNode node)
        {
            if (Prune(node)) return;
            ids[node] = Reserve();
            foreach (var kid in node.Kids.OfType<StructNode>()) Assign(kid);
        }

        Assign(root);
        foreach (var (node, id) in ids)
        {
            var kids = string.Join(" ", node.Kids.Select(k => k switch
            {
                StructNode child when ids.TryGetValue(child, out var childId) => $"{childId} 0 R",
                MarkedRef m => $"<</Type /MCR /Pg {pageIds[m.Page]} 0 R /MCID {m.Mcid}>>",
                _ => "",
            }).Where(k => k.Length > 0));
            var parent = node.Parent is { } p && ids.TryGetValue(p, out var parentId) ? $"{parentId} 0 R" : $"{structRoot} 0 R";
            var table = (node.Scope is { } scope ? $" /Scope /{scope}" : "") + (node.ColSpan > 1 ? $" /ColSpan {node.ColSpan}" : "");
            var attributes = table.Length > 0 ? $" /A <</O /Table{table}>>" : "";
            Set(id, $"<</Type /StructElem /S /{node.Type} /P {parent} /K [{kids}]{attributes}>>");
        }

        var nums = string.Join(" ", parentTree.Select((list, n) => $"{n} [{string.Join(" ", list.Select(node => $"{ids[node]} 0 R"))}]"));
        Set(structRoot, $"<</Type /StructTreeRoot /K [{ids[root]} 0 R] /ParentTree <</Nums [{nums}]>> /ParentTreeNextKey {pages.Count}>>");
        Set(pagesId, $"<</Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {pages.Count}>>");

        var title = document.Title.Length > 0 ? document.Title : "Letter";
        var xmp = $"""
            <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?>
            <x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
            <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:pdfuaid="http://www.aiim.org/pdfua/ns/id/">
            <dc:title><rdf:Alt><rdf:li xml:lang="x-default">{System.Security.SecurityElement.Escape(title)}</rdf:li></rdf:Alt></dc:title>
            <pdfuaid:part>1</pdfuaid:part>
            </rdf:Description></rdf:RDF></x:xmpmeta>
            <?xpacket end="w"?>
            """;
        var xmpBytes = Encoding.UTF8.GetBytes(xmp);
        Set(metadata, Concat(Encoding.ASCII.GetBytes($"<</Type /Metadata /Subtype /XML /Length {xmpBytes.Length}>>\nstream\n"), xmpBytes, "\nendstream"u8.ToArray()));
        Set(info, $"<</Title {Utf16(title)} /Producer (Heliograph experiment)>>");
        Set(catalog, $"<</Type /Catalog /Pages {pagesId} 0 R /StructTreeRoot {structRoot} 0 R /MarkInfo <</Marked true>> " +
            $"/Lang ({document.Language}) /Metadata {metadata} 0 R /ViewerPreferences <</DisplayDocTitle true>>>>");
        return Assemble(catalog, info);
    }

    // An element with nothing in it would only confuse a screen reader, except a table's cells and rows: an empty cell
    // still holds its place, or the rows no longer line up.
    private static bool Prune(StructNode node)
    {
        node.Kids.RemoveAll(k => k is StructNode child && Prune(child));
        return node.Kids.Count == 0 && node.Type is not ("Document" or "TD" or "TH" or "TR" or "Table");
    }

    private void WriteFont(TrueType face, int id, string name, Dictionary<ushort, string> glyphs)
    {
        var subset = face.Subset(glyphs.Keys);
        var tag = new string(SHA256.HashData(subset).Take(6).Select(b => (char)('A' + b % 26)).ToArray());
        var baseFont = $"{tag}+{face.PostScriptName}";
        var file = Reserve();
        Set(file, Concat(Encoding.ASCII.GetBytes($"<</Length {Compress(subset).Length} /Length1 {subset.Length} /Filter /FlateDecode>>\nstream\n"), Compress(subset), "\nendstream"u8.ToArray()));
        double S(int units) => units * 1000.0 / face.UnitsPerEm;
        var flags = 32 | (face.ItalicAngle != 0 ? 64 : 0);
        var descriptor = Reserve();
        Set(descriptor, $"<</Type /FontDescriptor /FontName /{baseFont} /Flags {flags} /FontBBox [{string.Join(" ", face.BBox.Select(b => N(S(b))))}] " +
            $"/ItalicAngle {N(face.ItalicAngle)} /Ascent {N(S(face.TypoAscender))} /Descent {N(S(face.TypoDescender))} /CapHeight {N(S(face.CapHeight))} " +
            $"/StemV {(face.Weight >= 600 ? 120 : 80)} /FontFile2 {file} 0 R>>");
        var widths = string.Join(" ", glyphs.Keys.Order().Select(g => $"{g} [{N(face.Advance(g))}]"));
        var cid = Reserve();
        Set(cid, $"<</Type /Font /Subtype /CIDFontType2 /BaseFont /{baseFont} /CIDSystemInfo <</Registry (Adobe) /Ordering (Identity) /Supplement 0>> " +
            $"/FontDescriptor {descriptor} 0 R /W [{widths}] /DW 1000 /CIDToGIDMap /Identity>>");
        var cmap = new StringBuilder("/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CIDSystemInfo <</Registry (Adobe) /Ordering (UCS) /Supplement 0>> def " +
            "/CMapName /Adobe-Identity-UCS def /CMapType 2 def 1 begincodespacerange <0000> <FFFF> endcodespacerange\n");
        foreach (var chunk in glyphs.OrderBy(g => g.Key).Chunk(100))
        {
            cmap.Append($"{chunk.Length} beginbfchar\n");
            foreach (var (glyph, text) in chunk) cmap.Append($"<{glyph:X4}> <{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))}>\n");
            cmap.Append("endbfchar\n");
        }

        cmap.Append("endcmap CMapName currentdict /CMap defineresource pop end end");
        var toUnicode = Stream(Encoding.ASCII.GetBytes(cmap.ToString()));
        Set(id, $"<</Type /Font /Subtype /Type0 /BaseFont /{baseFont} /Encoding /Identity-H /DescendantFonts [{cid} 0 R] /ToUnicode {toUnicode} 0 R>>");
    }

    private int Stream(byte[] data)
    {
        var compressed = Compress(data);
        var id = Reserve();
        Set(id, Concat(Encoding.ASCII.GetBytes($"<</Length {compressed.Length} /Filter /FlateDecode>>\nstream\n"), compressed, "\nendstream"u8.ToArray()));
        return id;
    }

    private static byte[] Compress(byte[] data)
    {
        var output = new MemoryStream();
        using (var z = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true)) z.Write(data);
        return output.ToArray();
    }

    private int Reserve()
    {
        _objects.Add([]);
        return _objects.Count;
    }

    private void Set(int id, string body) => _objects[id - 1] = Encoding.ASCII.GetBytes(body);
    private void Set(int id, byte[] body) => _objects[id - 1] = body;

    private byte[] Assemble(int catalog, int info)
    {
        var output = new MemoryStream();
        output.Write("%PDF-1.7\n%\xE2\xE3\xCF\xD3\n"u8);
        var offsets = new long[_objects.Count];
        for (var i = 0; i < _objects.Count; i++)
        {
            offsets[i] = output.Length;
            output.Write(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            output.Write(_objects[i]);
            output.Write("\nendobj\n"u8);
        }

        var xref = output.Length;
        var table = new StringBuilder($"xref\n0 {_objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) table.Append($"{o:D10} 00000 n \n");
        var hash = Convert.ToHexString(SHA256.HashData(output.ToArray()).AsSpan(0, 16));
        table.Append($"trailer\n<</Size {_objects.Count + 1} /Root {catalog} 0 R /Info {info} 0 R /ID [<{hash}> <{hash}>]>>\nstartxref\n{xref}\n%%EOF\n");
        output.Write(Encoding.ASCII.GetBytes(table.ToString()));
        return output.ToArray();
    }

    private static string Utf16(string text) => $"<FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))}>";
    private static string N(double value) => Math.Round(value, 3).ToString("0.###", Invariant);
    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
}
