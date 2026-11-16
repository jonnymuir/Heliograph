namespace Heliograph;

/// <summary>One problem, told the same way to a person and to an agent.</summary>
/// <param name="Code">A stable code from <see cref="DiagnosticCodes"/>.</param>
/// <param name="Message">What is wrong, in plain words.</param>
/// <param name="Fix">What to do about it.</param>
/// <param name="Location">Where it is, when it has a place in a file.</param>
/// <param name="Severity">An error stops the template or the render. A warning is something to look at.</param>
public sealed record Diagnostic(
    string Code, string Message, string Fix, DiagnosticLocation? Location = null, DiagnosticSeverity Severity = DiagnosticSeverity.Error);

public enum DiagnosticSeverity
{
    /// <summary>The template does not compile, or the document is not rendered.</summary>
    Error,

    /// <summary>Something an author should look at. It may be a mistake, and it does not stop anything.</summary>
    Warning,
}

/// <summary>A place in a document an author can find without tooling.</summary>
/// <param name="Part">In a document: "document", "header", "footer", "footnotes" or "endnotes". In a message: "sms", "email" or "email subject".</param>
/// <param name="Paragraph">The paragraph's position in that part, counting from 1. In a message, the line.</param>
/// <param name="Excerpt">The start of the paragraph's or line's text.</param>
public sealed record DiagnosticLocation(string Part, int Paragraph, string Excerpt);

/// <summary>The stable codes. HG0 is a refused file, HG1 a template problem, HG2 a problem with one recipient's data.</summary>
public static class DiagnosticCodes
{
    public const string NotAWordDocument = "HG0001";
    public const string ContainsMacros = "HG0002";
    public const string ContainsEmbeddedObject = "HG0003";
    public const string LoadsOutsideContent = "HG0004";
    public const string TooLarge = "HG0005";
    public const string HasTrackedChanges = "HG0006";
    public const string HasComments = "HG0007";
    public const string IsSigned = "HG0008";
    public const string StrictFormat = "HG0009";

    public const string UnknownField = "HG1001";
    public const string TagNotClosed = "HG1002";
    public const string TagHasNoName = "HG1003";
    public const string ConditionNotClosed = "HG1004";
    public const string EndWithoutCondition = "HG1005";
    public const string ConditionNeedsYesNo = "HG1006";
    public const string YesNoPrinted = "HG1007";
    public const string TagNotFilledIn = "HG1008";
    public const string LooksLikeATag = "HG1009";
    public const string HasHiddenText = "HG1010";
    public const string ConditionCutsThroughField = "HG1011";
    public const string ConditionHoldsSectionBreak = "HG1012";
    public const string WouldRenderBrokenDocument = "HG1013";
    public const string ListFieldOutsideRow = "HG1014";
    public const string RowMixesLists = "HG1015";
    public const string RowCannotRepeat = "HG1016";
    public const string SmsNeedsUnicode = "HG1017";
    public const string EmailSubjectMissing = "HG1018";
    public const string EmailLinkNotSafe = "HG1019";

    public const string ValueMissing = "HG2001";
    public const string ValueWrongType = "HG2002";
}

internal static class DiagnosticLists
{
    /// <summary>How many of the diagnostics stop the template. Warnings do not.</summary>
    public static int Errors(this IEnumerable<Diagnostic> diagnostics) => diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
}
