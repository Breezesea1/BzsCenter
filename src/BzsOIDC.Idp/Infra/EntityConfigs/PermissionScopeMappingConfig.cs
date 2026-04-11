using BzsOIDC.Idp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BzsOIDC.Idp.Infra.EntityConfigs;

internal sealed class PermissionScopeMappingConfig : IEntityTypeConfiguration<PermissionScopeMapping>
{
    /// <summary>
    /// 配置选项。
    /// </summary>
    /// <param name="builder">参数builder。</param>
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
