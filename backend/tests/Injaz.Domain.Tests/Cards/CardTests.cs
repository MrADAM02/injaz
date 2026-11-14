using Injaz.Domain.Cards;

namespace Injaz.Domain.Tests.Cards;

public sealed class CardTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private static Card NewCard() => Card.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "  Title  ", "V", Guid.CreateVersion7(), Now);

    [Fact]
    public void Due_dates_are_normalized_to_utc()
    {
        var card = NewCard();

        card.Schedule(new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.FromHours(3)), Now);

        Assert.Equal(new DateTimeOffset(2026, 12, 1, 14, 0, 0, TimeSpan.Zero), card.DueAt);
        Assert.Equal(TimeSpan.Zero, card.DueAt!.Value.Offset);
    }

    [Fact]
    public void Assignees_and_labels_are_sets()
    {
        var card = NewCard();
        var user = Guid.CreateVersion7();
        var label = Guid.CreateVersion7();

        Assert.True(card.Assign(user, Now));
        Assert.False(card.Assign(user, Now));
        Assert.True(card.AddLabel(label, Now));
        Assert.False(card.AddLabel(label, Now));
        Assert.Single(card.Assignees);
        Assert.Single(card.Labels);

        Assert.True(card.Unassign(user, Now));
        Assert.False(card.Unassign(user, Now));
        Assert.True(card.RemoveLabel(label, Now));
        Assert.False(card.RemoveLabel(label, Now));
    }

    [Fact]
    public void Blank_description_is_stored_as_none_and_title_is_trimmed()
    {
        var card = NewCard();

        card.Describe("   ", Now);

        Assert.Null(card.Description);
        Assert.Equal("Title", card.Title);
    }
}
