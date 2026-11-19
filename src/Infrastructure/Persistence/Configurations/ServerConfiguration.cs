using Domain.Servers;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class ServerConfiguration : IEntityTypeConfiguration<Server>
{
    public void Configure(EntityTypeBuilder<Server> server)
    {
        server.ToTable("Servers");
        server.HasKey(s => s.Id);
        server.Property(s => s.Id).ValueGeneratedNever();
        server.Property(s => s.Name).HasMaxLength(Server.NameMaxLength).IsRequired();
        server.Property(s => s.IconUrl).HasMaxLength(Server.IconUrlMaxLength);

        // Deleting a user who still owns a server must be decided explicitly, not cascade silently.
        server.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.OwnerId).OnDelete(DeleteBehavior.Restrict);
        server.HasIndex(s => s.OwnerId);

        // The aggregate owns its children; EF reads and writes them through the private backing lists.
        server.HasMany(s => s.Members).WithOne().HasForeignKey(m => m.ServerId).OnDelete(DeleteBehavior.Cascade);
        server.HasMany(s => s.Roles).WithOne().HasForeignKey(r => r.ServerId).OnDelete(DeleteBehavior.Cascade);
        server.HasMany(s => s.Channels).WithOne().HasForeignKey(c => c.ServerId).OnDelete(DeleteBehavior.Cascade);
        server.Navigation(s => s.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        server.Navigation(s => s.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
        server.Navigation(s => s.Channels).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ServerMemberConfiguration : IEntityTypeConfiguration<ServerMember>
{
    public void Configure(EntityTypeBuilder<ServerMember> member)
    {
        member.ToTable("ServerMembers");
        member.HasKey(m => new { m.ServerId, m.UserId });
        member.Property(m => m.Nickname).HasMaxLength(ServerMember.NicknameMaxLength);
        member.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        member.HasIndex(m => m.UserId); // "my servers"
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> role)
    {
        role.ToTable("Roles");
        role.HasKey(r => r.Id);
        role.Property(r => r.Id).ValueGeneratedNever();
        role.Property(r => r.Name).HasMaxLength(Role.NameMaxLength).IsRequired();
        role.HasIndex(r => r.ServerId);
        // Exactly one @everyone per server (no schema in the filter: it is just a column reference).
        role.HasIndex(r => r.ServerId, "IX_Roles_ServerId_Everyone").IsUnique().HasFilter("\"IsEveryone\"");
    }
}

internal sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> channel)
    {
        channel.ToTable("Channels");
        channel.HasKey(c => c.Id);
        channel.Property(c => c.Id).ValueGeneratedNever();
        channel.Property(c => c.Name).HasMaxLength(Channel.NameMaxLength).IsRequired();
        channel.Property(c => c.Topic).HasMaxLength(Channel.TopicMaxLength);
        channel.HasIndex(c => c.ServerId);
    }
}
