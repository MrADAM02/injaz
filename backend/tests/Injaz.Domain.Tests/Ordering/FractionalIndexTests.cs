using Injaz.Domain.Ordering;

namespace Injaz.Domain.Tests.Ordering;

public sealed class FractionalIndexTests
{
    [Fact]
    public void First_key_in_an_empty_sequence_is_short()
    {
        var key = FractionalIndex.Between(null, null);

        Assert.True(FractionalIndex.IsValid(key));
        Assert.Single(key);
    }

    [Theory]
    [InlineData(null, "V")]
    [InlineData("V", null)]
    [InlineData("V", "W")]
    [InlineData("V", "V1")]
    [InlineData("0V", "1")]
    [InlineData("a", "a01")]
    [InlineData("zzzz", null)]
    [InlineData(null, "001")]
    public void Between_returns_a_valid_key_strictly_between(string? after, string? before)
    {
        var key = FractionalIndex.Between(after, before);

        Assert.True(FractionalIndex.IsValid(key), key);
        if (after is not null)
        {
            Assert.True(string.CompareOrdinal(after, key) < 0, $"{after} < {key}");
        }

        if (before is not null)
        {
            Assert.True(string.CompareOrdinal(key, before) < 0, $"{key} < {before}");
        }
    }

    [Theory]
    [InlineData("b", "a")]
    [InlineData("a", "a")]
    [InlineData("a0", "b")]
    [InlineData("a!", null)]
    [InlineData("", null)]
    public void Between_rejects_misordered_or_malformed_keys(string after, string? before) =>
        Assert.Throws<ArgumentException>(() => FractionalIndex.Between(after, before));

    [Fact]
    public void Appending_hundreds_of_items_keeps_keys_short_and_ordered()
    {
        var keys = new List<string>();
        string? last = null;
        for (var i = 0; i < 500; i++)
        {
            last = FractionalIndex.Between(last, null);
            keys.Add(last);
        }

        AssertStrictlyAscending(keys);
        Assert.True(keys.Max(k => k.Length) <= 20, $"longest key: {keys.Max(k => k.Length)}");
    }

    [Fact]
    public void Prepending_hundreds_of_items_keeps_keys_short_and_ordered()
    {
        var keys = new List<string>();
        string? first = null;
        for (var i = 0; i < 500; i++)
        {
            first = FractionalIndex.Between(null, first);
            keys.Insert(0, first);
        }

        AssertStrictlyAscending(keys);
        Assert.All(keys, k => Assert.True(FractionalIndex.IsValid(k), k));
        Assert.True(keys.Max(k => k.Length) <= 40, $"longest key: {keys.Max(k => k.Length)}");
    }

    [Fact]
    public void Repeatedly_inserting_at_the_same_spot_grows_slowly_until_a_spread_is_needed()
    {
        string? low = FractionalIndex.Between(null, null);
        var high = FractionalIndex.Between(low, null);

        var inserts = 0;
        while (true)
        {
            var key = FractionalIndex.Between(low, high);
            if (key.Length > FractionalIndex.MaxLength)
            {
                break;
            }

            Assert.True(string.CompareOrdinal(low, key) < 0 && string.CompareOrdinal(key, high) < 0);
            high = key; // always insert right after "low": the worst case
            inserts++;
        }

        // ~6 inserts per extra character before the rebalance kicks in.
        Assert.True(inserts > 300, $"only {inserts} inserts before rebalance");
    }

    [Fact]
    public void Random_moves_never_break_the_order()
    {
        var random = new Random(42);
        var keys = new List<string> { FractionalIndex.Between(null, null) };

        for (var i = 0; i < 2_000; i++)
        {
            var index = random.Next(keys.Count + 1);
            var after = index > 0 ? keys[index - 1] : null;
            var before = index < keys.Count ? keys[index] : null;
            keys.Insert(index, FractionalIndex.Between(after, before));
        }

        AssertStrictlyAscending(keys);
        Assert.All(keys, k => Assert.True(FractionalIndex.IsValid(k) || k.Length > FractionalIndex.MaxLength, k));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(61)]
    [InlineData(1_000)]
    [InlineData(100_000)]
    public void Spread_produces_short_ascending_keys_with_room_between_them(int count)
    {
        var keys = FractionalIndex.Spread(count);

        Assert.Equal(count, keys.Count);
        AssertStrictlyAscending(keys);
        Assert.All(keys, k => Assert.True(FractionalIndex.IsValid(k), k));
        Assert.All(keys, k => Assert.True(k.Length <= 5, k));

        // Room before the first, between neighbours, and after the last.
        if (count > 1)
        {
            FractionalIndex.Between(null, keys[0]);
            FractionalIndex.Between(keys[0], keys[1]);
            FractionalIndex.Between(keys[^1], null);
        }
    }

    private static void AssertStrictlyAscending(IReadOnlyList<string> keys)
    {
        for (var i = 1; i < keys.Count; i++)
        {
            Assert.True(string.CompareOrdinal(keys[i - 1], keys[i]) < 0, $"'{keys[i - 1]}' !< '{keys[i]}' at {i}");
        }
    }
}
