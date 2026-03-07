using BzsCenter.Idp.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BzsCenter.Idp.Infra.EntityConfigs;

internal sealed class PermissionScopeMappingConfig : IEntityTypeConfiguration<PermissionScopeMapping>
{
    public void Configure(EntityTypeBuilder<PermissionScopeMapping> builder)
    {
        builder.ToTable("bzs_permission_scopes");
        builder.HasKey(static x => new { x.Permission, x.Scope });

        builder.Property(static x => x.Permission)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static x => x.Scope)
            .HasMaxLength(128)
            .IsRequired();
    }
}
