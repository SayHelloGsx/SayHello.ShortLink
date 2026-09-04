using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore;

[CollectionDefinition(WebHostTestConsts.CollectionDefinitionName)]
public class WebHostEntityFrameworkCoreCollection : ICollectionFixture<WebHostEntityFrameworkCoreFixture>
{

}
