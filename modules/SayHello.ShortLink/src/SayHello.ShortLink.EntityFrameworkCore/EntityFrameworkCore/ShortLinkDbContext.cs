using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace SayHello.ShortLink.EntityFrameworkCore;

[ConnectionStringName(ShortLinkDbProperties.ConnectionStringName)]
public class ShortLinkDbContext : AbpDbContext<ShortLinkDbContext>, IShortLinkDbContext
{
    public DbSet<ShortLinks.ShortLink> ShortLinks { get; set; } = null!;

    public DbSet<ShortLinkVisit> ShortLinkVisits { get; set; } = null!;

    public DbSet<ShortLinkDailyStatistic> ShortLinkDailyStatistics { get; set; } = null!;

    public DbSet<ShortLinkDailyDimensionStatistic> ShortLinkDailyDimensionStatistics { get; set; } = null!;

    public DbSet<BlockedDomain> BlockedDomains { get; set; } = null!;

    public ShortLinkDbContext(DbContextOptions<ShortLinkDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureShortLink();
    }
}
