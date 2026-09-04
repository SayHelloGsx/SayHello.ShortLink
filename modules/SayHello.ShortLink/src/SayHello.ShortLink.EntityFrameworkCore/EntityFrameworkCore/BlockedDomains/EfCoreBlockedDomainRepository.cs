using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.EntityFrameworkCore;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace SayHello.ShortLink.BlockedDomains;

public class EfCoreBlockedDomainRepository :
    EfCoreRepository<IShortLinkDbContext, BlockedDomain, Guid>,
    IBlockedDomainRepository
{
    public EfCoreBlockedDomainRepository(IDbContextProvider<IShortLinkDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<BlockedDomain>> GetListAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Domain)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<bool> IsBlockedAsync(
        string normalizedHost,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var domains = await (await GetDbSetAsync())
            .Where(x => x.IsActive && x.TenantId == tenantId)
            .Select(x => x.Domain)
            .ToListAsync(GetCancellationToken(cancellationToken));

        return domains.Any(domain => DomainNameNormalizer.IsSameOrSubdomainOf(normalizedHost, domain));
    }

    public async Task<bool> ExistsAsync(
        string normalizedDomain,
        Guid? tenantId,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).AnyAsync(
            x => x.Domain == normalizedDomain &&
                 x.TenantId == tenantId &&
                 (!excludingId.HasValue || x.Id != excludingId.Value),
            GetCancellationToken(cancellationToken));
    }
}
