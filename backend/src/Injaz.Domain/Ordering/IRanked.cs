namespace Injaz.Domain.Ordering;

/// <summary>An item positioned within a sequence by a <see cref="FractionalIndex"/> key. Order is (Rank, Id).</summary>
public interface IRanked
{
    Guid Id { get; }

    string Rank { get; }

    /// <summary>Only for rebalancing a whole sequence; moves go through the entity's own move method.</summary>
    void Rerank(string rank);
}
