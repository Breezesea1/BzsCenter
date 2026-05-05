using BzsOIDC.Idp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BzsOIDC.Idp.Infra.EntityConfigs;

internal sealed class ProtectedResourceConfig : IEntityTypeConfiguration<ProtectedResource>
{
    public void Configure(EntityTypeBuilder<ProtectedResource> builder)
    {
        builder.ToTable("bzs_protected_resources");
        builder.HasKey(static x => x.Id);

        builder.Property(static x => x.Id)
            .ValueGeneratedNever();

        builder.Property(static x => x.Key)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(static x => x.Key)
            .IsUnique();

        builder.Property(static x => x.DisplayName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(static x => x.Description)
            .HasMaxLength(1024);

        builder.Property(static x => x.IsActive)
            .IsRequired();

        builder.HasMany(static x => x.Permissions)
            .WithOne(static x => x.Resource)
            .HasForeignKey(static x => x.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(static x => x.Permissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
