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
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AsNoTracking()
            .OrderBy(x => x.Domain)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<bool> IsBlockedAsync(
        string normalizedHost,
        CancellationToken cancellationToken = default)
    {
        return await FindMatchingActiveAsync(
            normalizedHost,
            cancellationToken) is not null;
    }

    public async Task<BlockedDomain?> FindMatchingActiveAsync(
        string normalizedHost,
        CancellationToken cancellationToken = default)
    {
        var candidates = DomainNameNormalizer.GetParentCandidates(normalizedHost);

        return await (await GetDbSetAsync())
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                candidates.Contains(x.Domain))
            .OrderByDescending(x => x.Domain.Length)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<string>> GetExistingDomainsAsync(
        IReadOnlyCollection<string> normalizedDomains,
        CancellationToken cancellationToken = default)
    {
        if (normalizedDomains.Count == 0)
        {
            return [];
        }

        return await (await GetDbSetAsync())
            .AsNoTracking()
            .Where(x =>
                normalizedDomains.Contains(x.Domain))
            .Select(x => x.Domain)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<bool> ExistsAsync(
        string normalizedDomain,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).AnyAsync(
            x => x.Domain == normalizedDomain &&
                 (!excludingId.HasValue || x.Id != excludingId.Value),
            GetCancellationToken(cancellationToken));
    }
}
