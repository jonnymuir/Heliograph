using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Heliograph.Mcp;

/// <summary>
/// Ready-made prompts for the ways a content designer usually starts. A client shows them as starters (Claude Code
/// lists them as slash commands), so the working method travels with the server.
/// </summary>
[McpServerPromptType]
public static class CorrespondenceAuthoringPrompts
{
    [McpServerPrompt(Name = "write-a-correspondence")]
    [Description("Write the email and text message for a new correspondence from a plain-language brief, and say what the letter needs.")]
    public static string WriteACorrespondence(
        [Description("What is being sent, to whom, and when: for example \"tell an applicant their licence was refused, and how to appeal\".")] string brief,
        [Description("The name of the dictionary to use, from list_dictionaries.")] string dictionary) =>
        $"""
        Write this correspondence with Heliograph:

        {brief}

        Work like this:
        1. Read heliograph-skills://writing-a-template.
        2. Call read_dictionary for "{dictionary}". Tell me if the brief needs anything it does not list, before
           writing round the gap.
        3. Write the email and the text message, and save them with save_correspondence under a key you propose.
           Fix every problem it returns.
        4. Preview with made-up sample data and show me each part as a recipient would read it.
        5. Tell me what the letter should say, section by section, with the tags to use, so I can lay it out in my
           word processor and upload it.
        6. Do not publish. I will ask for that.
        """;

    [McpServerPrompt(Name = "fix-a-correspondence")]
    [Description("Work through what is wrong with a draft, fix what can be fixed, and explain the rest in plain words.")]
    public static string FixACorrespondence(
        [Description("The correspondence's key, from list_correspondence.")] string key) =>
        $"""
        The correspondence "{key}" has problems. Work through them:

        1. Call check_correspondence for "{key}" and read heliograph-skills://diagnostics for any code you do not know.
        2. Read the draft and its dictionary.
        3. Fix every problem in the email and the text message, and save. A misspelt tag is fixed by using the
           dictionary's name for the field, never by guessing a new one.
        4. For each problem in the document, tell me the paragraph, what is wrong and exactly what to change in my
           word processor. You cannot edit the document yourself.
        5. Check again, and tell me what is left.
        """;

    [McpServerPrompt(Name = "review-before-publishing")]
    [Description("Read a draft as its recipients would, for every outcome of its conditions, before it is published.")]
    public static string ReviewBeforePublishing(
        [Description("The correspondence's key, from list_correspondence.")] string key) =>
        $"""
        Review the draft of "{key}" before it is published.

        1. Check it. Stop and tell me if it has errors.
        2. Read its dictionary, and work out which yes or no fields its conditions use.
        3. Preview it once for each outcome that matters, with made-up data: every condition yes, every condition
           no, an empty list and a long one, and the longest names and addresses you can reasonably invent.
        4. For each preview, read the email and the text message as the recipient. Tell me about anything that
           reads wrongly, a sentence left hanging by a hidden section, a text message that runs to more parts
           than it should, and anything the letter, the email and the text say differently.
        5. Do not publish. Give me a short list of what to change, most important first.
        """;
}
