using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.ShortLinks;
using SayHello.ShortLink.EntityFrameworkCore;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.EntityFrameworkCore;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ReplaceDbContext(typeof(IShortLinkDbContext))]
[ReplaceDbContext(typeof(ISubscriptionDbContext))]
[ConnectionStringName("Default")]
public class WebHostDbContext :
    AbpDbContext<WebHostDbContext>,
    IIdentityDbContext,
    ITenantManagementDbContext,
    IShortLinkDbContext,
    ISubscriptionDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */

    #region Entities from the modules

    /* Notice: We only implemented IIdentityDbContext and ITenantManagementDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityDbContext and ITenantManagementDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    //Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }
    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    // ShortLink
    public DbSet<global::SayHello.ShortLink.ShortLinks.ShortLink> ShortLinks { get; set; }
    public DbSet<ShortLinkVisit> ShortLinkVisits { get; set; }
    public DbSet<ShortLinkDailyStatistic> ShortLinkDailyStatistics { get; set; }
    public DbSet<ShortLinkDailyDimensionStatistic> ShortLinkDailyDimensionStatistics { get; set; }
    public DbSet<BlockedDomain> BlockedDomains { get; set; }

    public DbSet<SubscriptionProduct> SubscriptionProducts { get; set; }
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<SubscriptionPlanEntitlement> SubscriptionPlanEntitlements { get; set; }
    public DbSet<SubscriptionBundle> SubscriptionBundles { get; set; }
    public DbSet<SubscriptionBundleItem> SubscriptionBundleItems { get; set; }
    public DbSet<UserSubscription> UserSubscriptions { get; set; }
    public DbSet<UserSubscriptionEntitlement> UserSubscriptionEntitlements { get; set; }

    #endregion

    public WebHostDbContext(DbContextOptions<WebHostDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureFeatureManagement();
        builder.ConfigureTenantManagement();
        builder.ConfigureShortLink();
        builder.ConfigureSubscription();

        /* Configure your own tables/entities inside here */

        //builder.Entity<YourEntity>(b =>
        //{
        //    b.ToTable(WebHostConsts.DbTablePrefix + "YourEntities", WebHostConsts.DbSchema);
        //    b.ConfigureByConvention(); //auto configure for the base class props
        //    //...
        //});
    }
}
