using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> token)
    {
        token.ToTable("RefreshTokens");
        token.HasKey(t => t.Id);
        token.Property(t => t.Id).ValueGeneratedNever();
        token.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        token.HasIndex(t => t.TokenHash).IsUnique();
        token.HasIndex(t => t.FamilyId);
        token.HasIndex(t => t.UserId);
        token.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
