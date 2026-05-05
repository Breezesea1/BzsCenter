using BzsOIDC.Idp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BzsOIDC.Idp.Infra.EntityConfigs;

internal sealed class PermissionDefinitionConfig : IEntityTypeConfiguration<PermissionDefinition>
{
    public void Configure(EntityTypeBuilder<PermissionDefinition> builder)
    {
        builder.ToTable("bzs_permission_definitions");
        builder.HasKey(static x => x.Id);

        builder.Property(static x => x.Id)
            .ValueGeneratedNever();

        builder.Property(static x => x.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(static x => x.Name)
            .IsUnique();

        builder.HasIndex(static x => new { x.ResourceId, x.Name })
            .IsUnique();

        builder.Property(static x => x.DisplayName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(static x => x.Description)
            .HasMaxLength(1024);

        builder.Property(static x => x.IsActive)
            .IsRequired();

        builder.HasMany(static x => x.ReleaseScopes)
            .WithOne(static x => x.Permission)
            .HasForeignKey(static x => x.PermissionDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(static x => x.ReleaseScopes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
