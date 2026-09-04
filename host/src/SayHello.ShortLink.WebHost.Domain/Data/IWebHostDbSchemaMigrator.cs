using System.Threading.Tasks;

namespace SayHello.ShortLink.WebHost.Data;

public interface IWebHostDbSchemaMigrator
{
    Task MigrateAsync();
}
