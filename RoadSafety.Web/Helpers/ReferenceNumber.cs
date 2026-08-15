using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace RoadSafety.Web.Helpers;

/// <summary>
/// Formatting and parsing for the project's identifiers — force numbers like
/// <c>ZP-00001</c> and reference numbers like <c>BR-001</c>.
///
/// Pure functions, so the numbering rules can be tested without a database.
/// <see cref="Services.NumberGenerator"/> is what reads the existing rows.
/// </summary>
public static class ReferenceNumber
{
    /// <summary>Builds an identifier, e.g. Format("BR", 7, 3) → "BR-007".</summary>
    public static string Format(string prefix, int sequence, int width) =>
        $"{prefix}-{sequence.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0')}";

    /// <summary>
    /// Reads the numeric tail of an identifier. Returns false for anything that
    /// does not carry the prefix or does not end in digits, so hand-entered
    /// oddities in the data cannot derail the next number.
    /// </summary>
    public static bool TryParseSequence(
        [NotNullWhen(true)] string? value, string prefix, out int sequence)
    {
        sequence = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var head = prefix + "-";
        if (!value.StartsWith(head, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var tail = value[head.Length..];

        return tail.Length > 0
            && tail.All(char.IsDigit)
            && int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out sequence);
    }

    /// <summary>
    /// The next identifier after the highest already in <paramref name="existing"/>.
    /// Gaps are not reused: if ZP-00004 was deleted, the next is still ZP-00006
    /// when ZP-00005 exists, so a retired number is never handed to someone new.
    /// </summary>
    public static string Next(IEnumerable<string?> existing, string prefix, int width)
    {
        var highest = 0;

        foreach (var value in existing)
        {
            if (TryParseSequence(value, prefix, out var sequence) && sequence > highest)
            {
                highest = sequence;
            }
        }

        return Format(prefix, highest + 1, width);
    }
}
