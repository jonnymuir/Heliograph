using Heliograph.Documents;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>Money and numbers are printed by their type. The data carries a plain number and the author writes no format.</summary>
public class MoneyAndNumberTests
{
    private static readonly FieldDictionary Dictionary = new(
    [
        new Field("Licence fee", "licenceFee", FieldType.Money),
        new Field("Number of jugglers", "numberOfJugglers", FieldType.Number),
    ]);

    [Theory]
    [InlineData("45", "45.00")]
    [InlineData("120.5", "120.50")]
    [InlineData("1234567.891", "1,234,567.89")]
    [InlineData("-12.5", "-12.50")]
    [InlineData("0", "0.00")]
    public void Money_is_printed_to_two_decimal_places_with_no_currency_sign(string amount, string printed)
    {
        Paragraphs("((Licence fee))", $$"""{ "licenceFee": {{amount}} }""").Should().Equal(printed);
    }

    [Fact]
    public void The_author_writes_the_currency_sign_and_it_is_never_changed()
    {
        using var stream = TestDocx.With("Pay £((Licence fee)) or $((Licence fee)).").Build();
        var document = DocumentTemplate.Compile(stream, Dictionary).Document!;
        var data = Benchmark.Data("""{ "licenceFee": 45 }""");

        foreach (var culture in new[] { "en-GB", "en-US", "ja-JP" })
        {
            var settings = new RenderSettings(System.Globalization.CultureInfo.GetCultureInfo(culture));
            TestDocx.Paragraphs(document.Render(data, settings).Docx!).Should().Equal("Pay £45.00 or $45.00.");
        }
    }

    [Theory]
    [InlineData("24", "24")]
    [InlineData("12500", "12,500")]
    [InlineData("2.5", "2.5")]
    [InlineData("-3", "-3")]
    public void A_number_is_printed_as_people_write_it(string number, string printed)
    {
        Paragraphs("((Number of jugglers))", $$"""{ "numberOfJugglers": {{number}} }""").Should().Equal(printed);
    }

    [Theory]
    [InlineData("""{ "licenceFee": "45" }""")]
    [InlineData("""{ "licenceFee": "£45.00" }""")]
    [InlineData("""{ "licenceFee": true }""")]
    public void Money_that_is_not_a_number_is_reported(string data)
    {
        Render("((Licence fee))", data).Diagnostics.Single().Code.Should().Be(DiagnosticCodes.ValueWrongType);
    }

    private static DocumentRender Render(string paragraph, string data)
    {
        using var stream = TestDocx.With(paragraph).Build();
        return DocumentTemplate.Compile(stream, Dictionary).Document!.Render(Benchmark.Data(data), Benchmark.British);
    }

    private static IReadOnlyList<string> Paragraphs(string paragraph, string data) => TestDocx.Paragraphs(Render(paragraph, data).Docx!);
}
