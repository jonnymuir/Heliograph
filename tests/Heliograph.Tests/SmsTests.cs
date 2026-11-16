using Heliograph.Messages;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>The SMS part of a correspondence: plain text with the same tags, and what it will cost to send.</summary>
public class SmsTests
{
    private static readonly FieldDictionary Dictionary = new(
    [
        new Field("Member name", "memberName", FieldType.Text),
        new Field("Amount owed", "amountOwed", FieldType.Money),
        new Field("Due date", "dueDate", FieldType.Date),
        new Field("Owes money", "owesMoney", FieldType.YesNo),
        new Field("Member address", "memberAddress", FieldType.Address),
        new Field("Payments", "payments", FieldType.List,
        [
            new Field("Paid on", "paidOn", FieldType.Date),
            new Field("Paid", "paid", FieldType.Money),
        ]),
        new Field("Refunds", "refunds", FieldType.List, [new Field("Refunded", "refunded", FieldType.Money)]),
    ]);

    [Theory]
    [InlineData("acknowledgement", "typical", "NJF: we have your licence application for Clubtown Summer Juggle. Ref NJF-2026-004217. We will reply by 14 December 2026.")]
    [InlineData("decision-letter", "approved", "NJF: your licence for Clubtown Summer Juggle is approved. Pay £45.00 by 9 January 2027. Details are in your letter.")]
    [InlineData("decision-letter", "refused", "NJF: we have made a decision on your licence application NJF-2026-004219. Please read the letter we have sent you.")]
    [InlineData("annual-statement", "typical", "NJF: your yearly contributions statement is on its way.")]
    [InlineData("annual-statement", "refund-and-arrears", "NJF: your yearly contributions statement is on its way. You owe £112.50, due by 4 January 2027.")]
    public void Each_benchmark_letter_has_a_text_message(string letter, string sample, string expected)
    {
        var compilation = SmsTemplate.Compile(File.ReadAllText(Benchmark.PathTo(letter, "sms.txt")), Benchmark.Dictionary(letter));
        compilation.Diagnostics.Should().BeEmpty();

        var render = compilation.Sms!.Render(Benchmark.Sample(letter, sample), Benchmark.British);

        render.Diagnostics.Should().BeEmpty();
        render.Text.Should().Be(expected);
    }

    [Fact]
    public void A_condition_on_lines_of_its_own_shows_or_hides_the_lines_between()
    {
        const string template = "Hello ((Member name)).\n((if Owes money))\nYou owe £((Amount owed)).\nPay by ((Due date)).\n((end if))\nBye.";

        Text(template, """{ "memberName": "Alex", "owesMoney": true, "amountOwed": 5, "dueDate": "2027-01-04" }""")
            .Should().Be("Hello Alex.\nYou owe £5.00.\nPay by 4 January 2027.\nBye.");
        Text(template, """{ "memberName": "Alex", "owesMoney": false }""").Should().Be("Hello Alex.\nBye.");
    }

    [Fact]
    public void A_line_that_was_all_condition_leaves_no_blank_line_when_hidden()
    {
        Text("Hello.\n((if Owes money))You owe us.((end if))\nBye.", """{ "owesMoney": false }""").Should().Be("Hello.\nBye.");
    }

    [Fact]
    public void A_line_that_uses_a_lists_fields_is_repeated_for_each_item()
    {
        const string data = """{ "payments": [ { "paidOn": "2026-01-02", "paid": 12.5 }, { "paidOn": "2026-02-02", "paid": 20 } ] }""";

        Text("Payments:\n((Paid on)): £((Paid))\nThanks.", data).Should().Be("Payments:\n2 January 2026: £12.50\n2 February 2026: £20.00\nThanks.");
    }

    [Fact]
    public void An_address_is_printed_one_line_under_another()
    {
        Text("To:\n((Member address))", """{ "memberAddress": ["12 Tumble Lane", "Clubtown"] }""").Should().Be("To:\n12 Tumble Lane\nClubtown");
    }

    [Fact]
    public void Text_that_looks_like_a_tag_stays_text()
    {
        Text("Hello ((Member name)).", """{ "memberName": "((Amount owed))" }""").Should().Be("Hello ((Amount owed)).");
    }

    [Theory]
    [InlineData(160, 1)]
    [InlineData(161, 2)]
    [InlineData(306, 2)]
    [InlineData(307, 3)]
    public void A_message_in_the_basic_character_set_is_counted_in_160s_then_153s(int length, int segments)
    {
        var render = Render(new string('a', length), "{}");

        render.Encoding.Should().Be(SmsEncoding.Gsm7);
        render.Segments.Should().Be(segments);
    }

    [Theory]
    [InlineData(70, 1)]
    [InlineData(71, 2)]
    [InlineData(134, 2)]
    [InlineData(135, 3)]
    public void A_message_with_a_character_outside_the_basic_set_is_counted_in_70s_then_67s(int length, int segments)
    {
        var render = Render("((Member name))", $$"""{ "memberName": "{{new string('a', length - 1)}}ł" }""");

        render.Encoding.Should().Be(SmsEncoding.Ucs2);
        render.Segments.Should().Be(segments);
    }

    [Fact]
    public void A_euro_sign_and_square_brackets_stay_in_the_basic_set_but_count_twice()
    {
        var render = Render(new string('a', 158) + "€", "{}");

        render.Encoding.Should().Be(SmsEncoding.Gsm7);
        render.Segments.Should().Be(1);
        Render(new string('a', 159) + "€", "{}").Segments.Should().Be(2);
    }

    [Fact]
    public void An_author_is_warned_when_the_template_itself_forces_the_costlier_encoding()
    {
        var compilation = SmsTemplate.Compile("Hello ((Member name)), it’s time to pay.", Dictionary);

        compilation.Succeeded.Should().BeTrue();
        var warning = compilation.Diagnostics.Should().ContainSingle().Subject;
        warning.Code.Should().Be(DiagnosticCodes.SmsNeedsUnicode);
        warning.Severity.Should().Be(DiagnosticSeverity.Warning);
        warning.Message.Should().Contain("’");
        warning.Location!.Part.Should().Be("sms");
    }

    [Fact]
    public void A_problem_says_which_line_of_the_message_it_is_on()
    {
        var diagnostic = SmsTemplate.Compile("Hello.\nDear ((Membr name)),", Dictionary).Diagnostics.Should().ContainSingle().Subject;

        diagnostic.Code.Should().Be(DiagnosticCodes.UnknownField);
        diagnostic.Fix.Should().Contain("((Member name))");
        diagnostic.Location.Should().Be(new DiagnosticLocation("sms", 2, "Dear ((Membr name)),"));
    }

    [Theory]
    [InlineData("((if Owes money))\nYou owe us.", DiagnosticCodes.ConditionNotClosed)]
    [InlineData("You owe us.\n((end if))", DiagnosticCodes.EndWithoutCondition)]
    [InlineData("Hi ((if Owes money)) you owe us.", DiagnosticCodes.ConditionNotClosed)]
    [InlineData("((Paid)) and ((Refunded))", DiagnosticCodes.RowMixesLists)]
    [InlineData("((Payments))", DiagnosticCodes.ListFieldOutsideRow)]
    [InlineData("((Owes money))", DiagnosticCodes.YesNoPrinted)]
    public void A_mistake_in_a_message_is_refused_the_way_it_is_in_a_document(string template, string code)
    {
        var compilation = SmsTemplate.Compile(template, Dictionary);

        compilation.Succeeded.Should().BeFalse();
        compilation.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(code);
    }

    [Fact]
    public void Missing_data_is_reported_and_no_message_is_produced()
    {
        var render = Render("Hello ((Member name)).", "{}");

        render.Text.Should().BeNull();
        render.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.ValueMissing);
    }

    [Fact]
    public void A_field_used_only_in_hidden_text_needs_no_value()
    {
        Text("Hi.((if Owes money)) You owe £((Amount owed)).((end if))", """{ "owesMoney": false }""").Should().Be("Hi.");
    }

    private static SmsRender Render(string template, string data)
    {
        var compilation = SmsTemplate.Compile(template, Dictionary);
        compilation.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        return compilation.Sms!.Render(Benchmark.Data(data), Benchmark.British);
    }

    private static string Text(string template, string data)
    {
        var render = Render(template, data);
        render.Diagnostics.Should().BeEmpty();
        return render.Text!;
    }
}
