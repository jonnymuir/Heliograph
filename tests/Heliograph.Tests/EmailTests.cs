using Heliograph.Messages;
using Heliograph.Tests.Support;

namespace Heliograph.Tests;

/// <summary>The email part of a correspondence: a subject and a body in a small, fixed set of Markdown.</summary>
public class EmailTests
{
    private static readonly FieldDictionary Dictionary = new(
    [
        new Field("Member name", "memberName", FieldType.Text),
        new Field("Notes", "notes", FieldType.Text),
        new Field("Reference", "reference", FieldType.Text),
        new Field("Amount owed", "amountOwed", FieldType.Money),
        new Field("Owes money", "owesMoney", FieldType.YesNo),
        new Field("Payments", "payments", FieldType.List,
        [
            new Field("Paid on", "paidOn", FieldType.Date),
            new Field("Paid", "paid", FieldType.Money),
        ]),
    ]);

    [Fact]
    public void The_acknowledgement_email_has_a_subject_a_heading_and_paragraphs()
    {
        var email = Benchmark("acknowledgement", "typical");

        email.Subject.Should().Be("We have received your application: NJF-2026-004217");
        email.Html.Should().Contain("<h1 ").And.Contain(">We have received your application</h1>");
        email.Html.Should().Contain(">Dear Alex Applicant,</p>");
        email.Html.Should().Contain("Licensing Team<br>National Juggling Federation</p>");
        email.Text.Should().StartWith("Dear Alex Applicant,\n\nWe have received your application\n\nWe received your application for a juggling event licence");
        email.Text.Should().EndWith("Licensing Team\nNational Juggling Federation");
    }

    [Theory]
    [InlineData("approved", "Your juggling event licence has been approved", "Your licence has been approved.", "We have refused")]
    [InlineData("refused", "A decision on your juggling event licence application", "We have refused your application.", "Pay the fee")]
    public void The_decision_email_changes_its_subject_and_body_with_the_outcome(string sample, string subject, string shown, string hidden)
    {
        var email = Benchmark("decision-letter", sample);

        email.Subject.Should().Be(subject);
        email.Text.Should().Contain(shown).And.NotContain(hidden).And.NotContain("\n\n\n");
        email.Html.Should().Contain(shown).And.NotContain(hidden);
    }

    [Fact]
    public void Bold_text_can_hold_a_value()
    {
        var email = Benchmark("decision-letter", "approved");

        email.Html.Should().Contain("Your licence number is <strong>JL-27-00381</strong>.");
        email.Text.Should().Contain("Your licence number is JL-27-00381.");
    }

    [Fact]
    public void The_statement_email_has_a_list_of_bullets_and_a_paragraph_only_for_those_who_owe()
    {
        var owing = Benchmark("annual-statement", "refund-and-arrears");

        owing.Html.Should().Contain("<ul ").And.Contain(">Total paid: £187.50</li>").And.Contain(">Amount due for the year: £300.00</li></ul>");
        owing.Text.Should().Contain("- Total paid: £187.50\n- Amount due for the year: £300.00\n\nYou owe £112.50. Pay by 4 January 2027");
        Benchmark("annual-statement", "typical").Text.Should().NotContain("You owe");
    }

    [Fact]
    public void The_html_is_a_whole_document_in_the_emails_language_with_the_subject_as_its_title()
    {
        var html = Email("Hello & welcome", "Dear ((Member name)),", """{ "memberName": "Alex" }""").Html!;

        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().Contain("<html lang=\"en-GB\">").And.Contain("<meta charset=\"utf-8\">").And.Contain("<title>Hello &amp; welcome</title>");
        html.Should().EndWith("</html>");
    }

    [Fact]
    public void Headings_lists_and_a_rule_become_real_structure()
    {
        var email = Email("S", "# Main\n## Second\n1. One\n2. Two\n---\n* Star\n- Dash", "{}");

        email.Html.Should().Contain(">Main</h1>").And.Contain(">Second</h2>");
        email.Html.Should().Contain("<ol ").And.Contain(">One</li>").And.Contain(">Two</li></ol>").And.Contain("<hr ");
        email.Html.Should().Contain(">Star</li>").And.Contain(">Dash</li></ul>");
        email.Text.Should().Be("Main\n\nSecond\n\n1. One\n2. Two\n\n---\n\n- Star\n- Dash");
    }

    [Fact]
    public void A_line_that_uses_a_lists_fields_becomes_a_bullet_for_each_item()
    {
        const string data = """{ "payments": [ { "paidOn": "2026-01-02", "paid": 12.5 }, { "paidOn": "2026-02-02", "paid": 20 } ] }""";

        var email = Email("S", "Your payments:\n\n- ((Paid on)): £((Paid))", data);

        email.Text.Should().Be("Your payments:\n\n- 2 January 2026: £12.50\n- 2 February 2026: £20.00");
        System.Text.RegularExpressions.Regex.Matches(email.Html!, "<li ").Should().HaveCount(2);
    }

    [Fact]
    public void A_link_has_its_text_and_its_address()
    {
        var email = Email("S", "Read [our guide](https://www.example.org/guide) today.", "{}");

        email.Html.Should().Contain("Read <a href=\"https://www.example.org/guide\"").And.Contain(">our guide</a> today.");
        email.Text.Should().Be("Read our guide: https://www.example.org/guide today.");
    }

    [Fact]
    public void A_value_in_a_links_address_is_encoded_so_it_cannot_change_where_the_link_goes()
    {
        var email = Email("S", "[Pay now](https://pay.example.org/((Reference))/start)", """{ "reference": "A/B?x=1&y=\"2\" #frag" }""");

        email.Html.Should().Contain("href=\"https://pay.example.org/A%2FB%3Fx%3D1%26y%3D%222%22%20%23frag/start\"");
    }

    [Fact]
    public void Text_from_the_data_cannot_become_markup_or_markdown()
    {
        var email = Email("S", "((Notes))\n\nNote: ((Notes))",
            """{ "notes": "# Not a heading <script>alert(1)</script> **not bold** [x](https://evil.example)" }""");

        email.Html.Should().NotContain("<script>").And.NotContain("<h1").And.NotContain("<strong>").And.NotContain("href=\"https://evil.example\"");
        email.Html.Should().Contain("# Not a heading &lt;script&gt;alert(1)&lt;/script&gt; **not bold** [x](https://evil.example)");
        email.Text.Should().Contain("Note: # Not a heading <script>alert(1)</script> **not bold** [x](https://evil.example)");
    }

    [Fact]
    public void A_line_break_in_supplied_text_is_a_line_break_in_the_email()
    {
        var email = Email("S", "((Notes))", """{ "notes": "One.\nTwo.\n\nThree." }""");

        email.Html.Should().Contain(">One.<br>Two.<br><br>Three.</p>");
        email.Text.Should().Be("One.\nTwo.\n\nThree.");
    }

    [Fact]
    public void A_condition_round_a_whole_link_is_allowed()
    {
        var compilation = EmailTemplate.Compile("S", "Hi.((if Owes money)) [Pay](https://pay.example.org)((end if))", Dictionary);

        compilation.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_blank_line_starts_a_new_paragraph_and_a_single_line_break_stays_in_the_same_one()
    {
        var email = Email("S", "One.\nTwo.\n\nThree.", "{}");

        email.Html.Should().Contain(">One.<br>Two.</p>").And.Contain(">Three.</p>");
        email.Text.Should().Be("One.\nTwo.\n\nThree.");
    }

    [Fact]
    public void A_subject_is_always_one_line_whatever_the_data_holds()
    {
        Email("Re: ((Notes))", "Hello.", """{ "notes": "one\r\nBcc: everyone@example.org\ttab" }""").Subject
            .Should().Be("Re: one Bcc: everyone@example.org tab");
    }

    [Fact]
    public void An_email_in_one_file_starts_with_its_subject_line_and_problems_give_the_files_line_numbers()
    {
        var compilation = EmailTemplate.Compile("Subject: Hello ((Member name))\n\nDear ((Membr name)),\n", Dictionary);

        var diagnostic = compilation.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.UnknownField);
        diagnostic.Location.Should().Be(new DiagnosticLocation("email", 3, "Dear ((Membr name)),"));
    }

    [Fact]
    public void A_problem_in_the_subject_says_so()
    {
        EmailTemplate.Compile("Hi ((Membr name))", "Hello.", Dictionary).Diagnostics.Single().Location!.Part.Should().Be("email subject");
    }

    [Theory]
    [InlineData("", "Hello.")]
    [InlineData("   ", "Hello.")]
    [InlineData("Two\nlines", "Hello.")]
    public void An_email_needs_a_subject_of_one_line(string subject, string body)
    {
        Refusal(EmailTemplate.Compile(subject, body, Dictionary)).Should().Be(DiagnosticCodes.EmailSubjectMissing);
    }

    [Fact]
    public void An_email_in_one_file_with_no_subject_line_is_refused()
    {
        Refusal(EmailTemplate.Compile("Dear ((Member name)),\n\nHello.", Dictionary)).Should().Be(DiagnosticCodes.EmailSubjectMissing);
    }

    [Theory]
    [InlineData("[click](javascript:alert(1))")]
    [InlineData("[click](data:text/html,hello)")]
    [InlineData("[click](((Reference)))")]
    [InlineData("[click](//evil.example/x)")]
    [InlineData("[click](((if Owes money))https://a.example((end if))javascript:alert(1))")]
    [InlineData("[click](https://a.example/((if Owes money))pay((end if)))")]
    public void A_link_that_does_not_plainly_go_to_a_web_page_an_email_address_or_a_phone_is_refused(string body)
    {
        Refusal(EmailTemplate.Compile("S", body, Dictionary)).Should().Be(DiagnosticCodes.EmailLinkNotSafe);
    }

    [Theory]
    [InlineData("[email us](mailto:help@example.org)")]
    [InlineData("[call us](tel:+441632960000)")]
    [InlineData("[site](http://www.example.org)")]
    public void A_link_to_an_email_address_a_phone_or_a_web_page_is_allowed(string body)
    {
        EmailTemplate.Compile("S", body, Dictionary).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_lists_field_cannot_go_in_the_subject()
    {
        Refusal(EmailTemplate.Compile("Paid ((Paid))", "Hello.", Dictionary)).Should().Be(DiagnosticCodes.ListFieldOutsideRow);
    }

    [Fact]
    public void Missing_data_is_reported_and_no_email_is_produced()
    {
        var render = EmailTemplate.Compile("S", "Dear ((Member name)),", Dictionary).Email!.Render(Tests.Support.Benchmark.Data("{}"), Tests.Support.Benchmark.British);

        render.Html.Should().BeNull();
        render.Subject.Should().BeNull();
        render.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.ValueMissing);
    }

    [Fact]
    public void The_same_template_and_data_give_the_same_email()
    {
        Email("S ((Member name))", "# Hi\n\nDear ((Member name)),", """{ "memberName": "Alex" }""").Html
            .Should().Be(Email("S ((Member name))", "# Hi\n\nDear ((Member name)),", """{ "memberName": "Alex" }""").Html);
    }

    private static string Refusal(EmailCompilation compilation)
    {
        compilation.Succeeded.Should().BeFalse();
        return compilation.Diagnostics.Should().ContainSingle().Subject.Code;
    }

    private static EmailRender Benchmark(string letter, string sample)
    {
        var compilation = EmailTemplate.Compile(File.ReadAllText(Tests.Support.Benchmark.PathTo(letter, "email.md")), Tests.Support.Benchmark.Dictionary(letter));
        compilation.Diagnostics.Should().BeEmpty();
        var render = compilation.Email!.Render(Tests.Support.Benchmark.Sample(letter, sample), Tests.Support.Benchmark.British);
        render.Diagnostics.Should().BeEmpty();
        return render;
    }

    private static EmailRender Email(string subject, string body, string data)
    {
        var compilation = EmailTemplate.Compile(subject, body, Dictionary);
        compilation.Diagnostics.Should().BeEmpty();
        var render = compilation.Email!.Render(Tests.Support.Benchmark.Data(data), Tests.Support.Benchmark.British);
        render.Diagnostics.Should().BeEmpty();
        return render;
    }
}
