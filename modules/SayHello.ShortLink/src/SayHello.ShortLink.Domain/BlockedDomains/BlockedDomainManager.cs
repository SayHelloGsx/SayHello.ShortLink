using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace SayHello.ShortLink.BlockedDomains;

public class BlockedDomainManager : DomainService
{
    private readonly IBlockedDomainRepository _repository;

    public BlockedDomainManager(IBlockedDomainRepository repository)
    {
        _repository = repository;
    }

    public virtual async Task<BlockedDomain> CreateAsync(
        string domain,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedDomain = BlockedDomainName.Normalize(domain);
        if (await _repository.ExistsAsync(
                normalizedDomain,
                cancellationToken: cancellationToken))
        {
            throw new BusinessException(ShortLinkErrorCodes.BlockedDomainAlreadyExists)
                .WithData("Host", normalizedDomain);
        }

        return new BlockedDomain(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            normalizedDomain,
            reason);
    }

    /// <summary>
    /// Unlike <see cref="CreateAsync"/>, already blocked domains are reported instead of failing
    /// the whole batch. Repeated domains keep their first occurrence.
    /// </summary>
    public virtual async Task<BlockedDomainBatchCreationResult> CreateManyAsync(
        IReadOnlyCollection<(string Domain, string? Reason)> domains,
        CancellationToken cancellationToken = default)
    {
        var requestedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requests = new List<(string Domain, string? Reason)>(domains.Count);
        foreach (var (domain, reason) in domains)
        {
            var normalizedDomain = BlockedDomainName.Normalize(domain);
            if (requestedDomains.Add(normalizedDomain))
            {
                requests.Add((normalizedDomain, reason));
            }
        }

        var existingDomains = requests.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _repository.GetExistingDomainsAsync(
                    requests.Select(x => x.Domain).ToList(),
                    cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = requests
            .Where(x => !existingDomains.Contains(x.Domain))
            .Select(x => new BlockedDomain(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                x.Domain,
                x.Reason))
            .ToList();

        return new BlockedDomainBatchCreationResult(created, existingDomains);
    }
}
