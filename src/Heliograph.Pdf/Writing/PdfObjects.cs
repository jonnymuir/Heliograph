using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Heliograph.Pdf.Writing;

/// <summary>The numbered objects of a PDF file, written out with a cross-reference table at the end.</summary>
internal sealed class PdfObjects
{
    private readonly List<byte[]> _objects = [];

    public int Reserve()
    {
        _objects.Add([]);
        return _objects.Count;
    }

    public void Set(int id, string body) => _objects[id - 1] = Encoding.ASCII.GetBytes(body);

    /// <summary>A stream object, compressed, with any extra dictionary entries given.</summary>
    public int Stream(byte[] data, string extra = "", bool compress = true)
    {
        var id = Reserve();
        var bytes = compress ? Compress(data) : data;
        var filter = compress ? " /Filter /FlateDecode" : "";
        _objects[id - 1] = [.. Encoding.ASCII.GetBytes($"<</Length {bytes.Length}{filter}{extra}>>\nstream\n"), .. bytes, .. "\nendstream"u8];
        return id;
    }

    public byte[] Assemble(int catalog, int info)
    {
        var output = new MemoryStream();
        output.Write("%PDF-1.7\n%âãÏÓ\n"u8);
        var offsets = new long[_objects.Count];
        for (var i = 0; i < _objects.Count; i++)
        {
            offsets[i] = output.Length;
            output.Write(Encoding.ASCII.GetBytes(Invariant($"{i + 1} 0 obj\n")));
            output.Write(_objects[i]);
            output.Write("\nendobj\n"u8);
        }

        var xref = output.Length;
        var table = new StringBuilder(Invariant($"xref\n0 {_objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets)
        {
            table.Append(Invariant($"{offset:D10} 00000 n \n"));
        }

        // The file's identifier comes from its own content, so the same letter always gives the same bytes.
        var id = Convert.ToHexString(SHA256.HashData(output.ToArray()).AsSpan(0, 16));
        table.Append(Invariant($"trailer\n<</Size {_objects.Count + 1} /Root {catalog} 0 R /Info {info} 0 R /ID [<{id}> <{id}>]>>\nstartxref\n{xref}\n%%EOF\n"));
        output.Write(Encoding.ASCII.GetBytes(table.ToString()));
        return output.ToArray();
    }

    public static byte[] Compress(byte[] data)
    {
        var output = new MemoryStream();
        using (var z = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(data);
        }

        return output.ToArray();
    }

    /// <summary>A number as PDF writes it: invariant, at most three decimals.</summary>
    public static string N(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Text as a PDF string in UTF-16, which every reader shows correctly.</summary>
    public static string Utf16(string text) => $"<FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))}>";

    public static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
