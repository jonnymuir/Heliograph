namespace Heliograph;

/// <summary>One problem, told the same way to a person and to an agent.</summary>
/// <param name="Code">A stable code from <see cref="DiagnosticCodes"/>.</param>
/// <param name="Message">What is wrong, in plain words.</param>
/// <param name="Fix">What to do about it.</param>
/// <param name="Location">Where it is, when it has a place in a file.</param>
public sealed record Diagnostic(string Code, string Message, string Fix, DiagnosticLocation? Location = null);

/// <summary>A place in a document an author can find without tooling.</summary>
/// <param name="Part">"document", "header" or "footer".</param>
/// <param name="Paragraph">The paragraph's position in that part, counting from 1.</param>
/// <param name="Excerpt">The start of the paragraph's text.</param>
public sealed record DiagnosticLocation(string Part, int Paragraph, string Excerpt);

/// <summary>The stable codes. HG0 is a refused file, HG1 a template problem, HG2 a problem with one recipient's data.</summary>
public static class DiagnosticCodes
{
    public const string NotAWordDocument = "HG0001";
    public const string ContainsMacros = "HG0002";
    public const string ContainsEmbeddedObject = "HG0003";
    public const string LoadsOutsideContent = "HG0004";
    public const string TooLarge = "HG0005";

    public const string UnknownField = "HG1001";
    public const string TagNotClosed = "HG1002";
    public const string TagHasNoName = "HG1003";
    public const string ConditionNotClosed = "HG1004";
    public const string EndWithoutCondition = "HG1005";
    public const string ConditionNeedsYesNo = "HG1006";
    public const string YesNoPrinted = "HG1007";

    public const string ValueMissing = "HG2001";
    public const string ValueWrongType = "HG2002";
}
