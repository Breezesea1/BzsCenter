using BzsCenter.Idp.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BzsCenter.Idp.Infra;

public sealed class IdpDbContext(DbContextOptions<IdpDbContext> options)
    : IdentityDbContext<BzsUser, BzsRole, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // 先调用基类以配置 Identity 模型
        base.OnModelCreating(builder);

        // 从当前程序集自动应用实体类型配置
        builder.ApplyConfigurationsFromAssembly(typeof(IdpDbContext).Assembly);
    }
}