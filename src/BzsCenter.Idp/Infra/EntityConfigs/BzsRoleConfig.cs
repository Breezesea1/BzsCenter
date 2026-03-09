using BzsCenter.Idp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BzsCenter.Idp.Infra.EntityConfigs;

public class BzsRoleConfig : IEntityTypeConfiguration<BzsRole>
{
    public void Configure(EntityTypeBuilder<BzsRole> builder)
    {
        builder.ToTable("bzs_roles");
    }
}
