using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>A table row that uses a list's fields repeats once for each item, with no marker.</summary>
public class ListTests
{
    private static readonly FieldDictionary Dictionary = new(
    [
        new Field("Member name", "memberName", FieldType.Text),
        new Field("Has payments", "hasPayments", FieldType.YesNo),
        new Field("Payments", "payments", FieldType.List,
        [
            new Field("Paid on", "paidOn", FieldType.Date),
            new Field("Paid", "paid", FieldType.Money),
            new Field("Note", "note", FieldType.Text),
            new Field("Was late", "wasLate", FieldType.YesNo),
        ]),
        new Field("Refunds", "refunds", FieldType.List, [new Field("Refunded", "refunded", FieldType.Money)]),
    ]);

    private const string TwoPayments = """
        { "memberName": "Alex", "hasPayments": true, "payments": [
            { "paidOn": "2026-01-02", "paid": 12.5, "note": "First", "wasLate": false },
            { "paidOn": "2026-02-02", "paid": 20, "note": "Second", "wasLate": true } ] }
        """;

    [Fact]
    public void The_row_repeats_for_each_item_and_the_rows_around_it_stay()
    {
        var docx = Table(Row("When", "How much"), Row("((Paid on))", "£((Paid))"), Row("End", ""));

        TestDocx.TableLines(Render(docx, TwoPayments)).Should().Equal("When | How much", "2 January 2026 | £12.50", "2 February 2026 | £20.00", "End | ");
    }

    [Fact]
    public void A_field_that_is_not_part_of_the_list_can_sit_in_a_repeated_row()
    {
        var docx = Table(Row("((Member name))", "((Paid))"));

        TestDocx.TableLines(Render(docx, TwoPayments)).Should().Equal("Alex | 12.50", "Alex | 20.00");
    }

    [Fact]
    public void A_condition_in_a_repeated_row_is_decided_item_by_item()
    {
        var docx = Table(Row("((Note))((if Was late)) (late)((end if))", "((Paid))"));

        TestDocx.TableRows(Render(docx, TwoPayments)).Select(row => row[0]).Should().Equal("First", "Second (late)");
    }

    [Fact]
    public void An_empty_list_leaves_the_other_rows_and_no_row_for_the_list()
    {
        var docx = Table(Row("When", "How much"), Row("((Paid on))", "((Paid))"));

        TestDocx.TableLines(Render(docx, """{ "payments": [] }""")).Should().Equal("When | How much");
    }

    [Fact]
    public void A_table_left_with_no_rows_at_all_is_removed()
    {
        var docx = TestDocx.With("Before.").RawParagraph(TableXml(Row("((Paid on))", "((Paid))"))).Paragraph("After.");

        var rendered = Render(docx, """{ "payments": [] }""");

        TestDocx.Paragraphs(rendered).Should().Equal("Before.", "After.");
        WordRules.Problems(rendered).Should().BeEmpty();
    }

    [Fact]
    public void A_list_inside_a_hidden_condition_needs_no_value()
    {
        var docx = TestDocx.With("((if Has payments))").RawParagraph(TableXml(Row("((Paid on))", "((Paid))"))).Paragraph("((end if))").Paragraph("Bye.");

        TestDocx.Paragraphs(Render(docx, """{ "hasPayments": false }""")).Should().Equal("Bye.");
    }

    [Fact]
    public void Text_with_a_blank_line_in_a_repeated_cell_becomes_paragraphs_in_that_cell()
    {
        var docx = Table(Row("((Note))", "((Paid))"));
        var data = """{ "payments": [ { "note": "One.\n\nTwo.", "paid": 1 } ] }""";

        TestDocx.Paragraphs(Render(docx, data)).Should().Equal("One.", "Two.", "1.00", "After the table.");
    }

    [Fact]
    public void A_missing_value_in_one_item_says_which_item()
    {
        var docx = Table(Row("((Paid on))", "((Paid))"));
        var data = """{ "payments": [ { "paidOn": "2026-01-02", "paid": 1 }, { "paidOn": "2026-02-02" } ] }""";

        var diagnostic = Problem(docx, data);

        diagnostic.Code.Should().Be(DiagnosticCodes.ValueMissing);
        diagnostic.Message.Should().Contain("Paid").And.Contain("item 2").And.Contain("Payments");
    }

    [Theory]
    [InlineData("""{ "payments": "none" }""")]
    [InlineData("""{ "payments": [ 12.5 ] }""")]
    public void A_list_that_is_not_a_list_of_items_is_reported(string data)
    {
        Problem(Table(Row("((Paid on))", "((Paid))")), data).Code.Should().Be(DiagnosticCodes.ValueWrongType);
    }

    [Fact]
    public void A_missing_list_is_reported_once()
    {
        Problem(Table(Row("((Paid on))", "((Paid))")), "{}").Code.Should().Be(DiagnosticCodes.ValueMissing);
    }

    [Fact]
    public void A_lists_field_outside_a_table_row_is_refused()
    {
        var diagnostic = Refusal(TestDocx.With("You paid ((Paid))."));

        diagnostic.Code.Should().Be(DiagnosticCodes.ListFieldOutsideRow);
        diagnostic.Message.Should().Contain("Paid").And.Contain("Payments");
        diagnostic.Location!.Paragraph.Should().Be(1);
    }

    [Fact]
    public void A_list_itself_cannot_be_printed()
    {
        Refusal(TestDocx.With("((Payments))")).Code.Should().Be(DiagnosticCodes.ListFieldOutsideRow);
    }

    [Fact]
    public void A_row_that_uses_two_lists_is_refused()
    {
        var diagnostic = Refusal(Table(Row("((Paid))", "((Refunded))")));

        diagnostic.Code.Should().Be(DiagnosticCodes.RowMixesLists);
        diagnostic.Message.Should().Contain("Payments").And.Contain("Refunds");
    }

    [Theory]
    [InlineData("<w:trPr><w:tblHeader/></w:trPr>", "", "heading row")]
    [InlineData("", "<w:vMerge w:val=\"restart\"/>", "merged")]
    public void A_row_that_cannot_sensibly_repeat_is_refused(string rowProperties, string cellProperties, string why)
    {
        var row = $"<w:tr>{rowProperties}<w:tc><w:tcPr><w:tcW w:w=\"2000\" w:type=\"dxa\"/>{cellProperties}</w:tcPr><w:p><w:r><w:t>((Paid))</w:t></w:r></w:p></w:tc></w:tr>";

        var diagnostic = Refusal(new TestDocx().RawParagraph(TableXml(row)).Paragraph("After."));

        diagnostic.Code.Should().Be(DiagnosticCodes.RowCannotRepeat);
        diagnostic.Message.Should().Contain(why);
    }

    [Fact]
    public void A_bookmark_in_a_repeated_row_is_not_copied_into_every_row()
    {
        var row = "<w:tr><w:tc><w:tcPr><w:tcW w:w=\"2000\" w:type=\"dxa\"/></w:tcPr><w:p><w:bookmarkStart w:id=\"3\" w:name=\"amount\"/>"
                  + "<w:r><w:t>((Paid))</w:t></w:r><w:bookmarkEnd w:id=\"3\"/></w:p></w:tc></w:tr>";

        var rendered = Render(new TestDocx().RawParagraph(TableXml(row)).Paragraph("After."), TwoPayments);

        TestDocx.PartText(rendered, "word/document.xml").Should().NotContain("bookmarkStart");
        WordRules.Problems(rendered).Should().BeEmpty();
    }

    private static string Row(params string[] cells) => "<w:tr>" + string.Concat(cells.Select(cell =>
        $"<w:tc><w:tcPr><w:tcW w:w=\"2000\" w:type=\"dxa\"/></w:tcPr><w:p><w:r><w:t xml:space=\"preserve\">{cell}</w:t></w:r></w:p></w:tc>")) + "</w:tr>";

    private static string TableXml(params string[] rows) =>
        "<w:tbl><w:tblPr><w:tblW w:w=\"0\" w:type=\"auto\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"2000\"/><w:gridCol w:w=\"2000\"/></w:tblGrid>" + string.Concat(rows) + "</w:tbl>";

    private static TestDocx Table(params string[] rows) => new TestDocx().RawParagraph(TableXml(rows)).Paragraph("After the table.");

    private static DocumentCompilation Compile(TestDocx docx)
    {
        using var stream = docx.Build();
        return DocumentTemplate.Compile(stream, Dictionary);
    }

    private static Diagnostic Refusal(TestDocx docx)
    {
        var compilation = Compile(docx);
        compilation.Succeeded.Should().BeFalse();
        return compilation.Diagnostics.Should().ContainSingle().Subject;
    }

    private static Diagnostic Problem(TestDocx docx, string data)
    {
        var compilation = Compile(docx);
        compilation.Diagnostics.Should().BeEmpty();
        var render = compilation.Document!.Render(Benchmark.Data(data), Benchmark.British);
        render.Docx.Should().BeNull();
        return render.Diagnostics.Should().ContainSingle().Subject;
    }

    private static byte[] Render(TestDocx docx, string data)
    {
        var compilation = Compile(docx);
        compilation.Diagnostics.Should().BeEmpty();
        var render = compilation.Document!.Render(Benchmark.Data(data), Benchmark.British);
        render.Diagnostics.Should().BeEmpty();
        return render.Docx!;
    }
}
