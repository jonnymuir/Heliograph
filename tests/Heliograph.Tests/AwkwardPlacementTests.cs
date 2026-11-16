using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>
/// What authors really do: change formatting half-way through a tag, or put a condition's tags where they would
/// break a table, a list or a link. Each case either works in a way that can be stated, or is refused with a reason.
/// </summary>
public class AwkwardPlacementTests
{
    private const string Hyperlink = "<w:hyperlink xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" r:id=\"rIdX0\">";

    private static readonly FieldDictionary Dictionary = new(
    [
        new Field("Applicant name", "applicantName", FieldType.Text),
        new Field("Owes money", "owesMoney", FieldType.YesNo),
    ]);

    private static readonly string Data = """{ "applicantName": "Alex Applicant", "owesMoney": false }""";

    [Fact]
    public void A_tag_whose_formatting_changes_half_way_prints_in_the_formatting_it_starts_with()
    {
        var docx = Render(new TestDocx().RawParagraph(
            "<w:p><w:r><w:t xml:space=\"preserve\">Dear ((Appli</w:t></w:r>"
            + "<w:r><w:rPr><w:i/></w:rPr><w:t xml:space=\"preserve\">cant name)) and friends</w:t></w:r></w:p>"));

        TestDocx.Paragraphs(docx).Should().Equal("Dear Alex Applicant and friends");
        TestDocx.ItalicText(docx).Should().Be(" and friends");
    }

    [Fact]
    public void A_tag_that_starts_in_italics_prints_in_italics_even_if_it_ends_in_plain_text()
    {
        var docx = Render(new TestDocx().RawParagraph(
            "<w:p><w:r><w:rPr><w:i/></w:rPr><w:t xml:space=\"preserve\">Dear ((Appli</w:t></w:r>"
            + "<w:r><w:t xml:space=\"preserve\">cant name)) and friends</w:t></w:r></w:p>"));

        TestDocx.ItalicText(docx).Should().Be("Dear Alex Applicant");
    }

    [Fact]
    public void A_tag_with_a_tab_or_a_spelling_mark_in_the_middle_of_it_is_still_read()
    {
        var docx = Render(new TestDocx().RawParagraph(
            "<w:p><w:r><w:t>((Applicant</w:t></w:r><w:proofErr w:type=\"spellStart\"/><w:r><w:t xml:space=\"preserve\"> na</w:t></w:r>"
            + "<w:proofErr w:type=\"spellEnd\"/><w:bookmarkStart w:id=\"1\" w:name=\"here\"/><w:r><w:t>me))</w:t></w:r><w:bookmarkEnd w:id=\"1\"/></w:p>"));

        TestDocx.Paragraphs(docx).Should().Equal("Alex Applicant");
    }

    [Fact]
    public void A_field_name_typed_with_a_non_breaking_space_is_still_found()
    {
        TestDocx.Paragraphs(Render(TestDocx.With("Dear ((Applicant name)),"))).Should().Equal("Dear Alex Applicant,");
    }

    [Fact]
    public void A_condition_that_opens_in_one_table_cell_and_closes_in_the_next_is_refused()
    {
        Refusal("<w:tbl><w:tr><w:tc><w:p><w:r><w:t>((if Owes money))</w:t></w:r></w:p></w:tc>"
                + "<w:tc><w:p><w:r><w:t>((end if))</w:t></w:r></w:p></w:tc></w:tr></w:tbl>")
            .Message.Should().Contain("table cell");
    }

    [Fact]
    public void A_condition_that_opens_in_one_table_row_and_closes_in_another_is_refused()
    {
        Refusal("<w:tbl><w:tr><w:tc><w:p><w:r><w:t>((if Owes money))</w:t></w:r></w:p></w:tc></w:tr>"
                + "<w:tr><w:tc><w:p><w:r><w:t>((end if))</w:t></w:r></w:p></w:tc></w:tr></w:tbl>")
            .Message.Should().Contain("table cell");
    }

    [Fact]
    public void A_condition_that_starts_inside_a_link_and_ends_outside_it_is_refused()
    {
        Refusal("<w:p>" + Hyperlink + "<w:r><w:t>((if Owes money))our website</w:t></w:r></w:hyperlink>"
                + "<w:r><w:t xml:space=\"preserve\"> today((end if)).</w:t></w:r></w:p>")
            .Message.Should().Contain("link");
    }

    [Fact]
    public void A_condition_wholly_inside_a_link_works()
    {
        var docx = Render(new TestDocx().RawParagraph(
            "<w:p><w:r><w:t xml:space=\"preserve\">See </w:t></w:r>" + Hyperlink
            + "<w:r><w:t>our website((if Owes money)) to pay((end if))</w:t></w:r></w:hyperlink><w:r><w:t>.</w:t></w:r></w:p>"));

        TestDocx.Paragraphs(docx).Should().Equal("See our website.");
    }

    [Fact]
    public void Hiding_some_items_of_a_bulleted_list_leaves_the_other_items_as_a_list()
    {
        const string bullet = "<w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr>";
        var docx = Render(new TestDocx().RawParagraph(
            $"<w:p>{bullet}<w:r><w:t>First</w:t></w:r></w:p><w:p>{bullet}<w:r><w:t>((if Owes money))</w:t></w:r></w:p>"
            + $"<w:p>{bullet}<w:r><w:t>Second</w:t></w:r></w:p><w:p>{bullet}<w:r><w:t>((end if))</w:t></w:r></w:p>"
            + $"<w:p>{bullet}<w:r><w:t>Third</w:t></w:r></w:p>"));

        TestDocx.Paragraphs(docx).Should().Equal("First", "Third");
    }

    /// <summary>The one diagnostic a template is refused with, which must say its condition is not closed.</summary>
    private static Diagnostic Refusal(string wordXml)
    {
        var diagnostic = Compile(new TestDocx().RawParagraph(wordXml)).Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.ConditionNotClosed);
        return diagnostic;
    }

    private static DocumentCompilation Compile(TestDocx docx)
    {
        using var stream = docx.Relationship(
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink", "https://www.example.org", external: true).Build();
        return DocumentTemplate.Compile(stream, Dictionary);
    }

    private static byte[] Render(TestDocx docx)
    {
        var compilation = Compile(docx);
        compilation.Diagnostics.Should().BeEmpty();
        var render = compilation.Document!.Render(Benchmark.Data(Data), Benchmark.British);
        render.Diagnostics.Should().BeEmpty();
        return render.Docx!;
    }
}
