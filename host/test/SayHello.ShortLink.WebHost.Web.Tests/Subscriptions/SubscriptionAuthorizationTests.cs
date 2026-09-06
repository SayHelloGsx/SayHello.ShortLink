using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SayHello.Subscription;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Subscriptions;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace SayHello.ShortLink.WebHost.Subscriptions;

public class SubscriptionAuthorizationTests : IClassFixture<SubscriptionAuthorizationFactory>
{
    private readonly SubscriptionAuthorizationFactory _factory;

    public SubscriptionAuthorizationTests(SubscriptionAuthorizationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/subscriptions/plans")]
    [InlineData("/subscriptions/bundles")]
    [InlineData("/api/subscription/public/products")]
    [InlineData("/api/subscription/public/plans")]
    [InlineData("/api/subscription/public/bundles")]
    public async Task Anonymous_Visitors_Should_See_Only_Public_Catalog(string route)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(route);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/subscriptions/mine")]
    [InlineData("/subscriptions/mine/674c1f08-f0be-49dd-9b62-bcd8da234d4b")]
    [InlineData("/api/subscription/public/mine")]
    [InlineData("/api/subscription/public/mine/674c1f08-f0be-49dd-9b62-bcd8da234d4b")]
    [InlineData("/api/subscription/public/entitlements/short-link")]
    [InlineData("/api/subscription/public/entitlements/short-link/boolean/statistics")]
    [InlineData("/api/subscription/public/entitlements/short-link/numeric/max-links")]
    [InlineData("/admin/subscriptions/products")]
    [InlineData("/admin/subscriptions/plans")]
    [InlineData("/admin/subscriptions/bundles")]
    [InlineData("/admin/subscriptions/users")]
    [InlineData("/api/subscription/admin/products")]
    [InlineData("/api/subscription/admin/plans")]
    [InlineData("/api/subscription/admin/bundles")]
    public async Task Anonymous_Visitors_Should_Not_Access_Private_Surfaces(string route)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(route);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/admin/subscriptions/products")]
    [InlineData("/admin/subscriptions/plans")]
    [InlineData("/admin/subscriptions/bundles")]
    [InlineData("/admin/subscriptions/users")]
    [InlineData("/api/subscription/admin/products")]
    [InlineData("/api/subscription/admin/plans")]
    [InlineData("/api/subscription/admin/bundles")]
    public async Task Authentication_Alone_Should_Not_Grant_Administrative_Permissions(string route)
    {
        using var client = CreateClient(authenticated: true);

        using var response = await client.GetAsync(route);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Authenticated_User_Should_Be_Able_To_View_Own_Subscriptions()
    {
        using var client = CreateClient(authenticated: true);

        using var response = await client.GetAsync("/subscriptions/mine");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task My_Subscriptions_APIs_Should_Ignore_Supplied_Owner_And_Return_Only_Current_User(bool otherOwner)
    {
        var owner = otherOwner ? SubscriptionAuthorizationFactory.OtherOwnerId : SubscriptionAuthorizationFactory.OwnerId;
        var expected = otherOwner ? _factory.OtherSubscriptionId : _factory.OwnerSubscriptionId;
        var suppliedOwner = otherOwner ? SubscriptionAuthorizationFactory.OwnerId : SubscriptionAuthorizationFactory.OtherOwnerId;
        using var client = CreateClient(authenticated: true, userId: owner);
        using var response = await client.GetAsync($"/api/subscription/public/mine?userId={suppliedOwner}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("totalCount").GetInt64().ShouldBe(1);
        json.RootElement.GetProperty("items").EnumerateArray().Single()
            .GetProperty("id").GetGuid().ShouldBe(expected);

        using var detail = await client.GetAsync($"/api/subscription/public/mine/{expected}");
        detail.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        detailJson.RootElement.GetProperty("id").GetGuid().ShouldBe(expected);
    }

    [Theory]
    [InlineData("/api/subscription/public/mine/")]
    [InlineData("/subscriptions/mine/")]
    public async Task Other_Users_Subscription_Details_Should_Be_Not_Found(string route)
    {
        using var client = CreateClient(authenticated: true);
        using var response = await client.GetAsync(route + _factory.OtherSubscriptionId);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/subscriptions/plans/")]
    [InlineData("/subscriptions/bundles/")]
    [InlineData("/api/subscription/public/products/")]
    [InlineData("/api/subscription/public/plans/")]
    [InlineData("/api/subscription/public/bundles/")]
    public async Task Unknown_Catalog_Details_Should_Be_Not_Found(string route)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(route + Guid.NewGuid());
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Effective_And_Typed_Entitlement_APIs_Should_Use_Current_Owner(bool otherOwner)
    {
        var owner = otherOwner ? SubscriptionAuthorizationFactory.OtherOwnerId : SubscriptionAuthorizationFactory.OwnerId;
        var expected = otherOwner ? _factory.OtherSubscriptionId : _factory.OwnerSubscriptionId;
        var suppliedOwner = otherOwner ? SubscriptionAuthorizationFactory.OwnerId : SubscriptionAuthorizationFactory.OtherOwnerId;
        using var client = CreateClient(authenticated: true, userId: owner);
        var prefix = "/api/subscription/public/entitlements/short-link";
        using var effective = await client.GetAsync($"{prefix}?userId={suppliedOwner}");
        effective.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var effectiveJson = JsonDocument.Parse(await effective.Content.ReadAsStringAsync());
        effectiveJson.RootElement.GetProperty("hasEffectiveSubscription").GetBoolean().ShouldBeTrue();
        effectiveJson.RootElement.GetProperty("subscription").GetProperty("id").GetGuid().ShouldBe(expected);

        using var boolean = await client.GetAsync($"{prefix}/boolean/statistics?userId={suppliedOwner}");
        boolean.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var booleanJson = JsonDocument.Parse(await boolean.Content.ReadAsStringAsync());
        booleanJson.RootElement.GetProperty("subscriptionId").GetGuid().ShouldBe(expected);
        booleanJson.RootElement.GetProperty("isGranted").GetBoolean().ShouldBe(!otherOwner);

        using var numeric = await client.GetAsync($"{prefix}/numeric/max-links?userId={suppliedOwner}");
        numeric.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var numericJson = JsonDocument.Parse(await numeric.Content.ReadAsStringAsync());
        numericJson.RootElement.GetProperty("subscriptionId").GetGuid().ShouldBe(expected);
        numericJson.RootElement.GetProperty("isGranted").GetBoolean().ShouldBeTrue();
        numericJson.RootElement.GetProperty("isUnlimited").GetBoolean().ShouldBe(otherOwner);
        if (otherOwner)
        {
            numericJson.RootElement.GetProperty("limit").ValueKind.ShouldBe(JsonValueKind.Null);
        }
        else
        {
            numericJson.RootElement.GetProperty("limit").GetInt64().ShouldBe(0);
        }
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task Valid_Admin_Mutations_Should_Require_Administrative_Permission(
        bool authenticated, HttpStatusCode expected)
    {
        using var client = CreateClient(authenticated);
        var input = new UpdateProductDto
        {
            Name = "Forbidden catalog edit",
            ConcurrencyStamp = _factory.ProductConcurrencyStamp
        };
        Validator.ValidateObject(input, new ValidationContext(input), validateAllProperties: true);
        using var response = await client.PutAsJsonAsync($"/api/subscription/admin/products/{_factory.ProductId}", input);
        response.StatusCode.ShouldBe(expected);

        using var product = await client.GetAsync($"/api/subscription/public/products/{_factory.ProductId}");
        product.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await product.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("name").GetString().ShouldBe("Authorization product");
    }

    [Fact]
    public void Authorization_Tests_Should_Not_Use_AlwaysAllow_Services()
    {
        _factory.Services.GetRequiredService<IAuthorizationService>()
            .ShouldBeAssignableTo<AbpAuthorizationService>();
        _factory.Services.GetRequiredService<IMethodInvocationAuthorizationService>()
            .ShouldBeAssignableTo<MethodInvocationAuthorizationService>();
        _factory.Services.GetRequiredService<IPermissionChecker>()
            .ShouldBeAssignableTo<PermissionChecker>();
    }

    private HttpClient CreateClient(bool authenticated = false, Guid? userId = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        if (authenticated)
        {
            client.DefaultRequestHeaders.Add(
                SubscriptionTestAuthenticationHandler.UserHeader,
                (userId ?? SubscriptionAuthorizationFactory.OwnerId).ToString("D"));
        }

        return client;
    }
}

public class SubscriptionAuthorizationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly Guid OwnerId = Guid.Parse("674c1f08-f0be-49dd-9b62-bcd8da234d4b");
    public static readonly Guid OtherOwnerId = Guid.Parse("31290197-a340-49b0-8db8-f69db516f878");
    public Guid ProductId { get; private set; }
    public string ProductConcurrencyStamp { get; private set; } = string.Empty;
    public Guid OwnerSubscriptionId { get; private set; }
    public Guid OtherSubscriptionId { get; private set; }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()
            .Begin(requiresNew: true, isTransactional: true);
        var users = scope.ServiceProvider.GetRequiredService<IdentityUserManager>();
        // Dynamic claims validates identities against the real user store; a fabricated ID becomes anonymous.
        foreach (var (id, name) in new[] { (OwnerId, "subscription-owner"), (OtherOwnerId, "subscription-other") })
        {
            var user = new IdentityUser(id, name, name + "@example.test");
            user.SetEmailConfirmed(true);
            var result = await users.CreateAsync(user);
            result.Succeeded.ShouldBeTrue();
        }
        await SeedSubscriptionsAsync(scope.ServiceProvider);
        await uow.CompleteAsync();
    }

    private async Task SeedSubscriptionsAsync(IServiceProvider services)
    {
        var definitions = services.GetRequiredService<ISubscriptionDefinitionRegistry>();
        var definition = definitions.GetProduct(ShortLinkSubscriptionDefinitions.ProductCode);
        var products = services.GetRequiredService<ISubscriptionProductRepository>();
        var product = await products.FindByCodeAsync(null, definition.Code);
        if (product == null)
        {
            product = new SubscriptionProduct(Guid.NewGuid(), null, definition, "Authorization product");
            product.Publish();
            await products.InsertAsync(product, autoSave: true);
        }
        else
        {
            product.UpdateDetails("Authorization product", null, 0);
            product.Publish();
            await products.UpdateAsync(product, autoSave: true);
        }
        ProductId = product.Id;
        ProductConcurrencyStamp = product.ConcurrencyStamp;
        var plans = services.GetRequiredService<ISubscriptionPlanRepository>();
        var subscriptions = services.GetRequiredService<IUserSubscriptionRepository>();
        var now = services.GetRequiredService<IClock>().Now.ToUniversalTime();
        foreach (var (owner, unlimited) in new[] { (OwnerId, false), (OtherOwnerId, true) })
        {
            var code = unlimited ? "public-auth-unlimited" : "public-auth-zero";
            var plan = new SubscriptionPlan(Guid.NewGuid(), product, code, code);
            plan.ReplaceEntitlements(definition, new Dictionary<string, EntitlementValue>
            {
                [ShortLinkSubscriptionDefinitions.Statistics] = EntitlementValue.Boolean(!unlimited),
                [ShortLinkSubscriptionDefinitions.MaxLinks] = unlimited ? EntitlementValue.Unlimited() : EntitlementValue.Numeric(0)
            });
            plan.Publish(product, definition);
            await plans.InsertAsync(plan, autoSave: true);
            var subscription = new UserSubscription(Guid.NewGuid(), owner, product, plan,
                plan.Entitlements.Select(e => new EntitlementSnapshotData(e.FeatureKey, e.FeatureKey, e.ToValue())).ToArray(),
                now.AddMinutes(-1), now.AddDays(7), Guid.NewGuid());
            await subscriptions.InsertAsync(subscription, autoSave: true);
            if (unlimited) OtherSubscriptionId = subscription.Id;
            else OwnerSubscriptionId = subscription.Id;
        }
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Transient<IAuthorizationService, AbpAuthorizationService>());
            services.Replace(ServiceDescriptor.Transient<IAbpAuthorizationService, AbpAuthorizationService>());
            services.Replace(ServiceDescriptor.Transient<IMethodInvocationAuthorizationService, MethodInvocationAuthorizationService>());
            services.Replace(ServiceDescriptor.Transient<IPermissionChecker, PermissionChecker>());
            services.Replace(ServiceDescriptor.Singleton<ICurrentPrincipalAccessor, SubscriptionHttpPrincipalAccessor>());
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = SubscriptionTestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = SubscriptionTestAuthenticationHandler.SchemeName;
                options.DefaultForbidScheme = SubscriptionTestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, SubscriptionTestAuthenticationHandler>(
                SubscriptionTestAuthenticationHandler.SchemeName, _ => { });
        });
    }
}

public class SubscriptionHttpPrincipalAccessor(IHttpContextAccessor accessor) : ThreadCurrentPrincipalAccessor
{
    protected override ClaimsPrincipal GetClaimsPrincipal() =>
        accessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
}

public class SubscriptionTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SubscriptionTest";
    public const string UserHeader = "X-Subscription-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var value))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Guid.TryParse(value.ToString(), out var userId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid subscription test user."));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(AbpClaimTypes.UserId, userId.ToString("D")),
                new Claim(AbpClaimTypes.UserName, "subscription-test-user")
            ],
            SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
