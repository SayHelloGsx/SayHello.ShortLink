using System;
using System.IO;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.WebHost.Web.HealthChecks;
using SayHello.ShortLink.WebHost.EntityFrameworkCore;
using SayHello.ShortLink.WebHost.Localization;
using SayHello.ShortLink.WebHost.MultiTenancy;
using SayHello.ShortLink.WebHost.Web.Menus;
using Microsoft.OpenApi;
using OpenIddict.Validation.AspNetCore;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Caching;
using Volo.Abp.Mapperly;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity.Web;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.Web;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement.Web;
using Volo.Abp.Swashbuckle;
using Volo.Abp.TenantManagement.Web;
using Volo.Abp.OpenIddict;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.UI;
using Volo.Abp.UI.Navigation;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.ShortLink.WebHost.Web;

[DependsOn(
    typeof(WebHostHttpApiModule),
    typeof(WebHostApplicationModule),
    typeof(WebHostEntityFrameworkCoreModule),
    typeof(global::SayHello.ShortLink.Web.ShortLinkWebModule),
    typeof(AbpAutofacModule),
    typeof(AbpIdentityWebModule),
    typeof(AbpSettingManagementWebModule),
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpTenantManagementWebModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule)
    )]
public class WebHostWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(
                typeof(WebHostResource),
                typeof(WebHostDomainModule).Assembly,
                typeof(WebHostDomainSharedModule).Assembly,
                typeof(WebHostApplicationModule).Assembly,
                typeof(WebHostApplicationContractsModule).Assembly,
                typeof(WebHostWebModule).Assembly
            );
        });

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("WebHost");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        if (!hostingEnvironment.IsDevelopment() && !hostingEnvironment.IsEnvironment("Testing"))
        {
            var certificatePath = configuration["OpenIddict:ServerCertificate:Path"];
            var certificatePassword = configuration["OpenIddict:ServerCertificate:Password"];
            if (certificatePath.IsNullOrWhiteSpace() || certificatePassword.IsNullOrWhiteSpace())
            {
                throw new AbpException(
                    "OpenIddict production certificate path and password must be configured.");
            }

            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.AddProductionEncryptionAndSigningCertificate(
                    certificatePath,
                    certificatePassword);
            });
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        ConfigureAuthentication(context);
        if (!hostingEnvironment.IsEnvironment("Testing"))
        {
            ConfigureRedis(context.Services, configuration);
        }
        else
        {
            context.Services.Replace(
                ServiceDescriptor.Singleton<
                    IShortLinkCreationRateLimiter,
                    InMemoryShortLinkCreationRateLimiter>());
            Configure<ShortLinkPrivacyOptions>(options =>
            {
                options.VisitorHashKey = "web-host-testing-visitor-hash-key-32-bytes";
            });
        }
        ConfigureRateLimiting(context.Services);
        ConfigureTransportSecurity(context.Services, configuration, hostingEnvironment);
        ConfigureHealthChecks(context.Services);
        ConfigureUrls(configuration);
        ConfigureBundles();
        ConfigureVirtualFileSystem(hostingEnvironment);
        ConfigureNavigationServices();
        ConfigureAutoApiControllers();
        ConfigureSwaggerServices(context.Services);

        context.Services.AddMapperlyObjectMapper<WebHostWebModule>();
    }

    private static void ConfigureHealthChecks(IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<PostgreSqlHealthCheck>("postgresql", tags: ["ready"])
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
    }

    private static void ConfigureTransportSecurity(
        IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment hostingEnvironment)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto |
                ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;

            var knownProxy = configuration["ReverseProxy:KnownProxy"];
            if (!knownProxy.IsNullOrWhiteSpace() &&
                System.Net.IPAddress.TryParse(knownProxy, out var proxyAddress))
            {
                options.KnownProxies.Add(proxyAddress);
            }
        });

        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
            options.Preload = true;
        });

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = hostingEnvironment.IsEnvironment("Testing")
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        services.AddAntiforgery(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = hostingEnvironment.IsEnvironment("Testing")
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
    }

    private static void ConfigureRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var path = httpContext.Request.Path;
                var remoteAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                if (path.StartsWithSegments("/Account/Register") ||
                    path.StartsWithSegments("/Account/ResendEmailConfirmation"))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"registration:{remoteAddress}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(10),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                }

                if (path.StartsWithSegments("/Account/Login"))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"login:{remoteAddress}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(5),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                }

                return RateLimitPartition.GetNoLimiter("unlimited");
            });
        });
    }

    private void ConfigureRedis(IServiceCollection services, IConfiguration configuration)
    {
        var redisConfiguration = configuration["Redis:Configuration"];
        if (redisConfiguration.IsNullOrWhiteSpace())
        {
            throw new AbpException("Redis:Configuration is required.");
        }

        var connectionMultiplexer = ConnectionMultiplexer.Connect(redisConfiguration);
        services.AddSingleton<IConnectionMultiplexer>(connectionMultiplexer);
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConfiguration;
        });
        services
            .AddDataProtection()
            .SetApplicationName("SayHello.ShortLink")
            .PersistKeysToStackExchangeRedis(
                connectionMultiplexer,
                "SayHello.ShortLink:DataProtectionKeys");

        Configure<AbpDistributedCacheOptions>(options =>
        {
            options.KeyPrefix = "SayHello.ShortLink:";
        });
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
        });
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );
        });
    }

    private void ConfigureVirtualFileSystem(IWebHostEnvironment hostingEnvironment)
    {
        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<WebHostDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}SayHello.ShortLink.WebHost.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<WebHostDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}SayHello.ShortLink.WebHost.Domain"));
                options.FileSets.ReplaceEmbeddedByPhysical<WebHostApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}SayHello.ShortLink.WebHost.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<WebHostApplicationModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}SayHello.ShortLink.WebHost.Application"));
                options.FileSets.ReplaceEmbeddedByPhysical<WebHostWebModule>(hostingEnvironment.ContentRootPath);
            });
        }
    }

    private void ConfigureNavigationServices()
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new WebHostMenuContributor());
        });
    }

    private void ConfigureAutoApiControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(WebHostApplicationModule).Assembly);
        });
    }

    private void ConfigureSwaggerServices(IServiceCollection services)
    {
        services.AddAbpSwaggerGen(
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "WebHost API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            }
        );
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseForwardedHeaders();

        if (!env.IsDevelopment() && !env.IsEnvironment("Testing"))
        {
            app.UseHsts();
        }

        if (!env.IsEnvironment("Testing"))
        {
            app.UseHttpsRedirection();
        }
        app.Use(async (httpContext, next) =>
        {
            httpContext.Response.OnStarting(() =>
            {
                var headers = httpContext.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers.ContentSecurityPolicy =
                    "default-src 'self'; " +
                    "base-uri 'self'; " +
                    "form-action 'self'; " +
                    "frame-ancestors 'none'; " +
                    "object-src 'none'; " +
                    "img-src 'self' data:; " +
                    "font-src 'self' data:; " +
                    "style-src 'self' 'unsafe-inline'; " +
                    "script-src 'self' 'unsafe-inline';";
                return Task.CompletedTask;
            });

            await next();
        });

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment() && !env.IsEnvironment("Testing"))
        {
            app.UseErrorPage();
        }

        app.UseCorrelationId();
        app.MapAbpStaticAssets();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();

        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseAbpSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "ShortLink API");
            });
        }

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints(endpoints =>
        {
            endpoints.MapHealthChecks(
                "/health/live",
                new HealthCheckOptions { Predicate = _ => false });
            endpoints.MapHealthChecks(
                "/health/ready",
                new HealthCheckOptions
                {
                    Predicate = registration => registration.Tags.Contains("ready")
                });
        });
    }
}
