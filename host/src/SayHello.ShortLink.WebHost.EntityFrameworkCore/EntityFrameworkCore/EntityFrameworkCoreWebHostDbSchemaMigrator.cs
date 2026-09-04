using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.WebHost.Data;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore;

public class EntityFrameworkCoreWebHostDbSchemaMigrator
    : IWebHostDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreWebHostDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolve the WebHostDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<WebHostDbContext>()
            .Database
            .MigrateAsync();
    }
}
