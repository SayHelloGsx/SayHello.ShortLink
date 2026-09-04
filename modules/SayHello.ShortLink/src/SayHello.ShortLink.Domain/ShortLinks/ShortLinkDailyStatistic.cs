using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkDailyStatistic : BasicAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid ShortLinkId { get; protected set; }

    public DateOnly Date { get; protected set; }

    public long VisitCount { get; protected set; }

    public long UniqueVisitorCount { get; protected set; }

    protected ShortLinkDailyStatistic()
    {
    }

    public ShortLinkDailyStatistic(
        Guid id,
        Guid? tenantId,
        Guid shortLinkId,
        DateOnly date,
        long visitCount,
        long uniqueVisitorCount)
        : base(id)
    {
        TenantId = tenantId;
        ShortLinkId = shortLinkId;
        Date = date;
        SetCounts(visitCount, uniqueVisitorCount);
    }

    public void SetCounts(long visitCount, long uniqueVisitorCount)
    {
        if (visitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitCount));
        }

        if (uniqueVisitorCount < 0 || uniqueVisitorCount > visitCount)
        {
            throw new ArgumentOutOfRangeException(nameof(uniqueVisitorCount));
        }

        VisitCount = visitCount;
        UniqueVisitorCount = uniqueVisitorCount;
    }
}
