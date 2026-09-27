using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentCenter.IdentityService.Domain.Entities;

namespace StudentCenter.IdentityService.Infrastructure.Persistence.Configurations;

public sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("UserPermissions");
        builder.HasKey(permission => new { permission.UserId, permission.Permission });
        builder.Property(permission => permission.Permission).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.HasOne(permission => permission.User).WithMany(user => user.Permissions).HasForeignKey(permission => permission.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
