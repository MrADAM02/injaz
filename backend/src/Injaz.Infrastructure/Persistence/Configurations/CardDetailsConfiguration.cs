using Injaz.Domain.Activity;
using Injaz.Domain.Boards;
using Injaz.Domain.Cards;
using Injaz.Domain.Users;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Injaz.Infrastructure.Persistence.Configurations;

internal sealed class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Text).HasMaxLength(ChecklistItem.TextMaxLength);
        builder.Property(i => i.Rank).IsRank();

        builder.HasIndex(i => new { i.CardId, i.Rank });
        builder.HasOne<Card>().WithMany().HasForeignKey(i => i.CardId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Body).HasMaxLength(Comment.BodyMaxLength);

        builder.HasIndex(c => new { c.CardId, c.CreatedAt, c.Id }).IsDescending(false, true, true);
        builder.HasOne<Card>().WithMany().HasForeignKey(c => c.CardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ActivityEntryConfiguration : IEntityTypeConfiguration<ActivityEntry>
{
    public void Configure(EntityTypeBuilder<ActivityEntry> builder)
    {
        builder.ToTable("activity");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Type).HasMaxLength(ActivityEntry.TypeMaxLength);
        builder.Property(a => a.Data).HasColumnType("jsonb");

        // Feed queries: newest first per board / per card.
        builder.HasIndex(a => new { a.BoardId, a.CreatedAt, a.Id }).IsDescending(false, true, true);
        builder.HasIndex(a => new { a.CardId, a.CreatedAt, a.Id }).IsDescending(false, true, true);

        builder.HasOne<Workspace>().WithMany().HasForeignKey(a => a.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Board>().WithMany().HasForeignKey(a => a.BoardId).OnDelete(DeleteBehavior.Cascade);

        // Deleting a card keeps the board's history of it (entries carry the title in Data).
        builder.HasOne<Card>().WithMany().HasForeignKey(a => a.CardId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
