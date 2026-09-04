using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace SayHello.ShortLink.EntityFrameworkCore;

[ConnectionStringName(ShortLinkDbProperties.ConnectionStringName)]
public interface IShortLinkDbContext : IEfCoreDbContext
{
    DbSet<ShortLinks.ShortLink> ShortLinks { get; }

    DbSet<ShortLinkVisit> ShortLinkVisits { get; }

    DbSet<ShortLinkDailyStatistic> ShortLinkDailyStatistics { get; }

    DbSet<ShortLinkDailyDimensionStatistic> ShortLinkDailyDimensionStatistics { get; }

    DbSet<BlockedDomain> BlockedDomains { get; }
}
