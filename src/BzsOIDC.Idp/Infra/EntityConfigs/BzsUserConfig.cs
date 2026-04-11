using BzsOIDC.Idp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BzsOIDC.Idp.Infra.EntityConfigs;

internal sealed class BzsUserConfig : IEntityTypeConfiguration<BzsUser>
{
    public void Configure(EntityTypeBuilder<BzsUser> builder)
    {
        builder.ToTable("bzs_users");
        builder.Property(t => t.DisplayName)
            .IsUnicode()
            .HasMaxLength(64)
            .IsRequired();
    }
}
