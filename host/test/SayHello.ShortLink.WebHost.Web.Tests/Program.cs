using Microsoft.AspNetCore.Builder;
using SayHello.ShortLink.WebHost;
using Volo.Abp.AspNetCore.TestBase;

var builder = WebApplication.CreateBuilder();

builder.Environment.EnvironmentName = "Testing";
builder.Environment.ContentRootPath = GetWebProjectContentRootPathHelper.Get("SayHello.ShortLink.WebHost.Web.csproj");
await builder.RunAbpModuleAsync<WebHostWebTestModule>(applicationName: "SayHello.ShortLink.WebHost.Web" );

public partial class Program
{
}
