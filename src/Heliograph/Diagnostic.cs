namespace Heliograph;

/// <summary>One problem, told the same way to a person and to an agent.</summary>
/// <param name="Code">A stable code from <see cref="DiagnosticCodes"/>.</param>
/// <param name="Message">What is wrong, in plain words.</param>
/// <param name="Fix">What to do about it.</param>
/// <param name="Location">Where it is, when it has a place in a file.</param>
/// <param name="Severity">An error stops the template or the render. A warning is something to look at.</param>
public sealed record Diagnostic(
    string Code, string Message, string Fix, DiagnosticLocation? Location = null, DiagnosticSeverity Severity = DiagnosticSeverity.Error);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DiagnosticSeverity>))]
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

/// <summary>
/// The stable codes. HG0 is a refused file, HG1 a template problem, HG2 a problem with one recipient's data, HG3 a
/// problem with stored correspondence, HG4 something in a template that the host's PDF route cannot lay out.
/// </summary>
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
    public const string DocumentHasNoTitle = "HG1020";
    public const string TagSheetLeftIn = "HG1021";

    public const string ValueMissing = "HG2001";
    public const string ValueWrongType = "HG2002";

    public const string CorrespondenceNotFound = "HG3001";
    public const string NotPublished = "HG3002";
    public const string DictionaryNotFound = "HG3003";
    public const string DictionaryNotValid = "HG3004";
    public const string NoParts = "HG3005";
    public const string ChangedElsewhere = "HG3006";
    public const string KeyNotValid = "HG3007";
    public const string DictionaryNameNotValid = "HG3008";
    public const string DictionariesReadOnly = "HG3009";
    public const string SampleNotValid = "HG3010";

    public const string PdfCannotDrawPicture = "HG4001";
    public const string PdfCannotDrawShape = "HG4002";
    public const string PdfCannotLayOutNotes = "HG4003";
    public const string PdfCannotLayOutSections = "HG4004";
    public const string PdfCannotLayOutColumns = "HG4005";
    public const string PdfCannotWorkOutField = "HG4006";
    public const string PdfCannotMakeLink = "HG4007";
    public const string PdfCannotDrawFormatting = "HG4008";
    public const string PdfCannotLayOutDirection = "HG4009";
    public const string PdfCannotLayOutTable = "HG4010";
    public const string PdfCannotAlignTab = "HG4011";
    public const string PdfCannotLayOutEvenPages = "HG4012";
    public const string PdfFontDrawnInLookAlike = "HG4013";
    public const string PdfCannotDrawListLabel = "HG4014";
}

internal static class DiagnosticLists
{
    /// <summary>How many of the diagnostics stop the template. Warnings do not.</summary>
    public static int Errors(this IEnumerable<Diagnostic> diagnostics) => diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
}
