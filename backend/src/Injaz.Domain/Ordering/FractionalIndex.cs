namespace Injaz.Domain.Ordering;

/// <summary>
/// Fractional ordering keys ("lexorank-style"). A key is a base-62 fraction written as a string of digits
/// <c>0-9A-Za-z</c>; because those characters are in ascending ASCII order, comparing keys with ordinal string
/// comparison (PostgreSQL <c>COLLATE "C"</c>) gives the same order as comparing the fractions.
/// <para>
/// Moving an item only needs a key strictly between its new neighbours — no other row changes. Keys never end
/// in the smallest digit, which guarantees there is always room for another key before any key.
/// </para>
/// </summary>
public static class FractionalIndex
{
    /// <summary>Longest key we accept before asking the caller to <see cref="Spread"/> the sequence again.</summary>
    public const int MaxLength = 64;

    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int Base = 62;

    /// <summary>
    /// Returns a key strictly between <paramref name="after"/> and <paramref name="before"/>.
    /// Null means "no neighbour on that side" (start or end of the sequence).
    /// </summary>
    /// <exception cref="ArgumentException">A key is malformed, or <paramref name="after"/> is not less than <paramref name="before"/>.</exception>
    public static string Between(string? after, string? before)
    {
        if (after is not null)
        {
            Validate(after, nameof(after));
        }

        if (before is not null)
        {
            Validate(before, nameof(before));
        }

        if (after is not null && before is not null && string.CompareOrdinal(after, before) >= 0)
        {
            throw new ArgumentException($"'{after}' must sort before '{before}'.", nameof(after));
        }

        return (after, before) switch
        {
            (null, null) => Digits[Base / 2].ToString(),
            (not null, null) => Increment(after),
            (null, not null) => Decrement(before),
            _ => Midpoint(after!, before),
        };
    }

    /// <summary>
    /// Appending is the most common move (new card at the bottom), so instead of halving the remaining space — which
    /// adds a digit every ~6 appends — bump the last digit by one: a digit is only added every ~36 appends.
    /// </summary>
    private static string Increment(string key)
    {
        var last = Digits.IndexOf(key[^1], StringComparison.Ordinal);
        return last < Base - 1
            ? key[..^1] + Digits[last + 1]
            : key + Digits[Base / 2];
    }

    /// <summary>Mirror of <see cref="Increment"/> for prepending; never produces a trailing smallest digit.</summary>
    private static string Decrement(string key)
    {
        var last = Digits.IndexOf(key[^1], StringComparison.Ordinal);
        return last > 1
            ? key[..^1] + Digits[last - 1]
            : key[..^1] + Digits[0] + Digits[Base / 2];
    }

    /// <summary>
    /// <paramref name="count"/> evenly spaced, short, ascending keys — used to rebalance one sequence when keys have
    /// grown too long or collided. All keys have at most <c>ceil(log62(count + 1)) + 1</c> digits.
    /// </summary>
    public static IReadOnlyList<string> Spread(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var length = 1;
        long space = Base;
        while (space < (count + 1L) * 2 && length < 10)
        {
            length++;
            space *= Base;
        }

        var step = space / (count + 1L);
        var keys = new string[count];
        for (var i = 0; i < count; i++)
        {
            keys[i] = Encode(step * (i + 1), length);
        }

        return keys;
    }

    public static bool IsValid(string? key) =>
        !string.IsNullOrEmpty(key)
        && key.Length <= MaxLength
        && key[^1] != Digits[0]
        && key.All(c => Digits.Contains(c, StringComparison.Ordinal));

    private static string Midpoint(string a, string? b)
    {
        // Shared prefix: the answer must share it too; recurse on the remainder.
        if (b is not null)
        {
            var n = 0;
            while (n < b.Length && (n < a.Length ? a[n] : Digits[0]) == b[n])
            {
                n++;
            }

            if (n > 0)
            {
                return b[..n] + Midpoint(n < a.Length ? a[n..] : string.Empty, b[n..]);
            }
        }

        var digitA = a.Length > 0 ? Digits.IndexOf(a[0], StringComparison.Ordinal) : 0;
        var digitB = b is not null ? Digits.IndexOf(b[0], StringComparison.Ordinal) : Base;

        if (digitB - digitA > 1)
        {
            // Room for a single digit in between.
            return Digits[(digitA + digitB + 1) / 2].ToString();
        }

        // Adjacent first digits.
        if (b is not null && b.Length > 1)
        {
            // b's first digit alone is already > a and < b (b continues with non-zero digits).
            return b[..1];
        }

        // Keep a's first digit and find something after the rest of a.
        return Digits[digitA] + Midpoint(a.Length > 1 ? a[1..] : string.Empty, null);
    }

    private static string Encode(long value, int length)
    {
        var chars = new char[length];
        for (var i = length - 1; i >= 0; i--)
        {
            chars[i] = Digits[(int)(value % Base)];
            value /= Base;
        }

        return new string(chars).TrimEnd(Digits[0]);
    }

    private static void Validate(string key, string paramName)
    {
        if (!IsValid(key))
        {
            throw new ArgumentException($"'{key}' is not a valid ordering key.", paramName);
        }
    }
}
