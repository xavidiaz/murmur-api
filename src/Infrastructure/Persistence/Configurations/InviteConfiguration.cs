using Domain.Invites;
using Domain.Servers;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> invite)
    {
        invite.ToTable("Invites");
        invite.HasKey(i => i.Code); // the primary key makes codes unique; a collision on insert is retried with a new code
        invite.Property(i => i.Code).HasMaxLength(InviteCode.Length).ValueGeneratedNever();

        // An invite dies with its server.
        invite.HasOne<Server>().WithMany().HasForeignKey(i => i.ServerId).OnDelete(DeleteBehavior.Cascade);
        // CreatorId is non-nullable in the contract (InviteDto.CreatorId), so SET NULL is out; deleting a user must
        // decide explicitly what happens to what they created (as with Servers.OwnerId), so no silent cascade either.
        invite.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.CreatorId).OnDelete(DeleteBehavior.Restrict);

        invite.HasIndex(i => i.ServerId);
        invite.HasIndex(i => i.CreatorId);
    }
}
