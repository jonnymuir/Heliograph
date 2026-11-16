namespace Heliograph.Messages;

/// <summary>How a text message is carried, which decides how much fits in one.</summary>
public enum SmsEncoding
{
    /// <summary>The basic character set (GSM 03.38). 160 characters in one message, 153 in each part of a longer one.</summary>
    Gsm7,

    /// <summary>Needed when any character is outside the basic set. 70 characters in one message, 67 in each part of a longer one.</summary>
    Ucs2,
}

/// <summary>Works out how a message will be carried and how many messages it will be charged as.</summary>
internal static class SmsLength
{
    // The basic character set of GSM 03.38, and the characters that take two places in it.
    private const string Basic = "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?"
                                 + "¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    private const string Extended = "^{}\\[~]|€";

    /// <summary>The first character that cannot be sent in the basic set, or null if all can.</summary>
    public static char? FirstOutsideBasicSet(string text)
    {
        var index = text.AsSpan().IndexOfAnyExcept(Basic + Extended);
        return index < 0 ? null : text[index];
    }

    public static (SmsEncoding Encoding, int Segments) Measure(string text)
    {
        if (FirstOutsideBasicSet(text) is not null)
        {
            return (SmsEncoding.Ucs2, Segments(text.Length, single: 70, part: 67));
        }

        var places = text.Length + text.Count(Extended.Contains);
        return (SmsEncoding.Gsm7, Segments(places, single: 160, part: 153));
    }

    private static int Segments(int length, int single, int part) => length <= single ? 1 : (length + part - 1) / part;
}
