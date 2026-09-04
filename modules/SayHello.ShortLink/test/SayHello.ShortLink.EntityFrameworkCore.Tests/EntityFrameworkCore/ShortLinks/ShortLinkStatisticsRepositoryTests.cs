using System;
using System.Linq;
using System.Threading.Tasks;
using SayHello.ShortLink.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkStatisticsRepositoryTests : ShortLinkEntityFrameworkCoreTestBase
{
    private readonly IShortLinkRepository _shortLinkRepository;
    private readonly IShortLinkStatisticsRepository _statisticsRepository;
    private readonly IRepository<ShortLinkVisit, Guid> _visitRepository;
    private readonly IRepository<ShortLinkDailyStatistic, Guid> _dailyRepository;
    private readonly IRepository<ShortLinkDailyDimensionStatistic, Guid> _dimensionRepository;
    private readonly IGuidGenerator _guidGenerator;

    public ShortLinkStatisticsRepositoryTests()
    {
        _shortLinkRepository = GetRequiredService<IShortLinkRepository>();
        _statisticsRepository = GetRequiredService<IShortLinkStatisticsRepository>();
        _visitRepository = GetRequiredService<IRepository<ShortLinkVisit, Guid>>();
        _dailyRepository = GetRequiredService<IRepository<ShortLinkDailyStatistic, Guid>>();
        _dimensionRepository =
            GetRequiredService<IRepository<ShortLinkDailyDimensionStatistic, Guid>>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task GetAsync_Should_Combine_Rollups_And_Raw_Visits()
    {
        var link = new ShortLink(
            _guidGenerator.Create(),
            null,
            Guid.NewGuid(),
            "Stats01",
            "https://example.com/",
            null,
            null);
        var firstDay = new DateOnly(2026, 9, 1);
        var secondDay = firstDay.AddDays(1);

        await WithUnitOfWorkAsync(async () =>
        {
            await _shortLinkRepository.InsertAsync(link, autoSave: true);
            await _dailyRepository.InsertAsync(
                new ShortLinkDailyStatistic(
                    _guidGenerator.Create(),
                    null,
                    link.Id,
                    firstDay,
                    2,
                    2),
                autoSave: true);
            await InsertDimensionAsync(
                link.Id,
                firstDay,
                StatisticDimension.ReferrerHost,
                "example.com",
                2);
            await InsertDimensionAsync(
                link.Id,
                firstDay,
                StatisticDimension.Browser,
                "Chrome",
                2);
            await InsertDimensionAsync(
                link.Id,
                firstDay,
                StatisticDimension.DeviceType,
                "Desktop",
                2);

            await InsertVisitAsync(
                link.Id,
                secondDay,
                new string('A', ShortLinkConsts.VisitorHashLength),
                null,
                "Chrome",
                "Mobile");
            await InsertVisitAsync(
                link.Id,
                secondDay,
                new string('A', ShortLinkConsts.VisitorHashLength),
                "example.com",
                "Chrome",
                "Mobile");
            await InsertVisitAsync(
                link.Id,
                secondDay,
                new string('B', ShortLinkConsts.VisitorHashLength),
                "example.com",
                "Firefox",
                "Mobile");
            await InsertVisitAsync(
                link.Id,
                firstDay.AddDays(3),
                new string('C', ShortLinkConsts.VisitorHashLength),
                "outside.example",
                "Edge",
                "Tablet");
        });

        var statistics = await WithUnitOfWorkAsync(() =>
            _statisticsRepository.GetAsync(
                link.Id,
                firstDay,
                firstDay.AddDays(2),
                10));

        statistics.UniqueVisitorCount.ShouldBe(4);
        statistics.Daily.Select(x => (x.Date, x.VisitCount, x.UniqueVisitorCount))
            .ShouldBe(
            [
                (firstDay, 2L, 2L),
                (secondDay, 3L, 2L),
                (firstDay.AddDays(2), 0L, 0L)
            ]);
        statistics.Referrers.Select(x => (x.Value, x.VisitCount))
            .ShouldBe([("example.com", 4L), ("Direct", 1L)]);
        statistics.Browsers.Select(x => (x.Value, x.VisitCount))
            .ShouldBe([("Chrome", 4L), ("Firefox", 1L)]);
        statistics.Devices.Select(x => (x.Value, x.VisitCount))
            .ShouldBe([("Mobile", 3L), ("Desktop", 2L)]);
    }

    private Task InsertDimensionAsync(
        Guid shortLinkId,
        DateOnly date,
        StatisticDimension dimension,
        string value,
        long visitCount)
    {
        return _dimensionRepository.InsertAsync(
            new ShortLinkDailyDimensionStatistic(
                _guidGenerator.Create(),
                null,
                shortLinkId,
                date,
                dimension,
                value,
                visitCount),
            autoSave: true);
    }

    private Task InsertVisitAsync(
        Guid shortLinkId,
        DateOnly date,
        string visitorHash,
        string? referrerHost,
        string browser,
        string deviceType)
    {
        return _visitRepository.InsertAsync(
            new ShortLinkVisit(
                _guidGenerator.Create(),
                null,
                shortLinkId,
                date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
                visitorHash,
                referrerHost,
                browser,
                deviceType),
            autoSave: true);
    }
}
