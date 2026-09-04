using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.WebHost.Data;

/* This is used if database provider does't define
 * IWebHostDbSchemaMigrator implementation.
 */
public class NullWebHostDbSchemaMigrator : IWebHostDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
