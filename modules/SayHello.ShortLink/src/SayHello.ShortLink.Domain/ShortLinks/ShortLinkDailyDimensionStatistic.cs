using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkDailyDimensionStatistic : BasicAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid ShortLinkId { get; protected set; }

    public DateOnly Date { get; protected set; }

    public StatisticDimension Dimension { get; protected set; }

    public string Value { get; protected set; } = string.Empty;

    public long VisitCount { get; protected set; }

    protected ShortLinkDailyDimensionStatistic()
    {
    }

    public ShortLinkDailyDimensionStatistic(
        Guid id,
        Guid? tenantId,
        Guid shortLinkId,
        DateOnly date,
        StatisticDimension dimension,
        string value,
        long visitCount)
        : base(id)
    {
        if (visitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitCount));
        }

        TenantId = tenantId;
        ShortLinkId = shortLinkId;
        Date = date;
        Dimension = dimension;
        Value = Check.NotNullOrWhiteSpace(
            value,
            nameof(value),
            ShortLinkConsts.MaxDimensionValueLength);
        VisitCount = visitCount;
    }

    public void SetVisitCount(long visitCount)
    {
        if (visitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitCount));
        }

        VisitCount = visitCount;
    }
}
