using BzsOIDC.Idp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BzsOIDC.Idp.Infra.EntityConfigs;

internal sealed class PermissionReleaseScopeConfig : IEntityTypeConfiguration<PermissionReleaseScope>
{
    public void Configure(EntityTypeBuilder<PermissionReleaseScope> builder)
    {
        builder.ToTable("bzs_permission_release_scopes");
        builder.HasKey(static x => x.Id);

        builder.Property(static x => x.Id)
            .ValueGeneratedNever();

        builder.Property(static x => x.Scope)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(static x => new { x.PermissionDefinitionId, x.Scope })
            .IsUnique();
    }
}
