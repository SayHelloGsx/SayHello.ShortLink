using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SayHello.Subscription.EntityFrameworkCore;

public class SubscriptionTimestampTests : SubscriptionPersistenceTestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lifecycle_timestamps_roundtrip_as_UTC_without_global_UTC_clock_configuration(bool permanent)
    {
        var data = await SeedAsync();
        Assert.Equal(DateTimeKind.Unspecified, TestClock.Clock.Kind);
        Assert.False(TestClock.Clock.SupportsMultipleTimezone);
        TestClock.Now = DateTime.SpecifyKind(TestClock.Now, DateTimeKind.Local);
        var startsAt = TestClock.Now.ToUniversalTime();
        var expiresAt = permanent ? null : (DateTime?)startsAt.AddHours(1);
        var assigned = await AssignPlanAsync(data, 0, expiresAt);

        var stored = await InTransactionAsync(() => Subscriptions.GetAsync(null, assigned.Id));
        Assert.Equal(startsAt, stored.StartsAt);
        Assert.Equal(DateTimeKind.Utc, stored.StartsAt.Kind);
        Assert.Equal(expiresAt, stored.ExpiresAt);
        if (!permanent) Assert.Equal(DateTimeKind.Utc, stored.ExpiresAt!.Value.Kind);
        Assert.Null(stored.EndedAt);

        TestClock.Now = TestClock.Now.AddMinutes(15);
        await InTransactionAsync(() => Manager.RevokeAsync(null, stored.Id, stored.ConcurrencyStamp, "history"));
        var history = await InTransactionAsync(() => Subscriptions.GetAsync(null, assigned.Id));
        Assert.Equal(DateTimeKind.Utc, history.StartsAt.Kind);
        Assert.Equal(expiresAt, history.ExpiresAt);
        if (!permanent) Assert.Equal(DateTimeKind.Utc, history.ExpiresAt!.Value.Kind);
        Assert.Equal(TestClock.Now.ToUniversalTime(), history.EndedAt);
        Assert.Equal(DateTimeKind.Utc, history.EndedAt!.Value.Kind);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            history.StartsAt,
            history.ExpiresAt,
            history.EndedAt
        }));
        Assert.EndsWith("Z", json.RootElement.GetProperty("StartsAt").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("Z", json.RootElement.GetProperty("EndedAt").GetString(), StringComparison.Ordinal);
        if (permanent)
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("ExpiresAt").ValueKind);
        else
            Assert.EndsWith("Z", json.RootElement.GetProperty("ExpiresAt").GetString(), StringComparison.Ordinal);
    }
}
