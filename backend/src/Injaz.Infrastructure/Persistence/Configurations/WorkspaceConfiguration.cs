using Injaz.Domain.Users;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Injaz.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Name).HasMaxLength(Workspace.NameMaxLength);

        builder.HasOne<User>().WithMany().HasForeignKey(w => w.CreatedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.Members)
            .WithOne()
            .HasForeignKey(m => m.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(w => w.Members).HasField("_members").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Aggregate-level optimistic concurrency: every membership change touches the workspace row.
        builder.Property<uint>("Version").IsRowVersion();
    }
}

internal sealed class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.HasKey(m => new { m.WorkspaceId, m.UserId });
        builder.HasIndex(m => m.UserId); // "my workspaces"

        // Stored as a number so SQL can order and compare roles (Owner > Admin > Member > Viewer).
        builder.Property(m => m.Role).HasConversion<short>();

        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Email).HasMaxLength(User.EmailMaxLength);
        builder.Property(i => i.Role).HasConversion<short>();
        builder.Property(i => i.TokenHash).HasMaxLength(64);

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.WorkspaceId, i.Email });

        builder.HasOne<Workspace>().WithMany().HasForeignKey(i => i.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.InvitedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.AcceptedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
