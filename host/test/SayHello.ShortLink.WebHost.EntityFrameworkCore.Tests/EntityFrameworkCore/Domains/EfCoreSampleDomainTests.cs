using SayHello.ShortLink.WebHost.Samples;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.Domains;

[Collection(WebHostTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<WebHostEntityFrameworkCoreTestModule>
{

}
