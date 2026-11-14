using Injaz.Domain.Boards;
using Injaz.Domain.Cards;
using Injaz.Domain.Users;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Injaz.Infrastructure.Persistence.Configurations;

/// <summary>Shared mapping for fractional ranks: byte-order collation so SQL ordering equals ordinal string order.</summary>
internal static class RankMapping
{
    public const string Collation = "C";

    public static PropertyBuilder<string> IsRank(this PropertyBuilder<string> property) =>
        property.HasMaxLength(128).UseCollation(Collation);
}

internal sealed class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Title).HasMaxLength(Board.TitleMaxLength);
        builder.Property(b => b.Description).HasMaxLength(Board.DescriptionMaxLength);
        builder.Property(b => b.Color).HasMaxLength(16);

        builder.HasIndex(b => b.WorkspaceId);
        builder.HasOne<Workspace>().WithMany().HasForeignKey(b => b.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(b => b.CreatedById).OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("Version").IsRowVersion();
    }
}

internal sealed class BoardListConfiguration : IEntityTypeConfiguration<BoardList>
{
    public void Configure(EntityTypeBuilder<BoardList> builder)
    {
        builder.ToTable("lists");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Title).HasMaxLength(BoardList.TitleMaxLength);
        builder.Property(l => l.Rank).IsRank();

        builder.HasIndex(l => new { l.BoardId, l.Rank });
        builder.HasOne<Board>().WithMany().HasForeignKey(l => l.BoardId).OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("Version").IsRowVersion();
    }
}

internal sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Name).HasMaxLength(Label.NameMaxLength);
        builder.Property(l => l.Color).HasMaxLength(16);

        builder.HasIndex(l => l.BoardId);
        builder.HasOne<Board>().WithMany().HasForeignKey(l => l.BoardId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Title).HasMaxLength(Card.TitleMaxLength);
        builder.Property(c => c.Description).HasMaxLength(Card.DescriptionMaxLength);
        builder.Property(c => c.Rank).IsRank();
        builder.Property(c => c.Priority).HasConversion<short>();
        builder.Ignore(c => c.IsCompleted);

        builder.HasIndex(c => new { c.ListId, c.Rank });
        builder.HasIndex(c => c.BoardId);

        builder.HasOne<Board>().WithMany().HasForeignKey(c => c.BoardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<BoardList>().WithMany().HasForeignKey(c => c.ListId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Assignees).WithOne().HasForeignKey(a => a.CardId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Assignees).HasField("_assignees").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => c.Labels).WithOne().HasForeignKey(l => l.CardId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Labels).HasField("_labels").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<uint>("Version").IsRowVersion();
    }
}

internal sealed class CardAssigneeConfiguration : IEntityTypeConfiguration<CardAssignee>
{
    public void Configure(EntityTypeBuilder<CardAssignee> builder)
    {
        builder.ToTable("card_assignees");
        builder.HasKey(a => new { a.CardId, a.UserId });
        builder.HasIndex(a => a.UserId); // "My Tasks"
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CardLabelConfiguration : IEntityTypeConfiguration<CardLabel>
{
    public void Configure(EntityTypeBuilder<CardLabel> builder)
    {
        builder.ToTable("card_labels");
        builder.HasKey(l => new { l.CardId, l.LabelId });
        builder.HasIndex(l => l.LabelId);
        builder.HasOne<Label>().WithMany().HasForeignKey(l => l.LabelId).OnDelete(DeleteBehavior.Cascade);
    }
}
