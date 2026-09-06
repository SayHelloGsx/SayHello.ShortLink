using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SayHello.Subscription.Public.Localization;
using Volo.Abp.UI.Navigation;
using Volo.Abp.Users;

namespace SayHello.Subscription.Public.Web.Menus;

public class SubscriptionPublicMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name != StandardMenus.Main)
        {
            return Task.CompletedTask;
        }

        var l = context.GetLocalizer<SubscriptionPublicResource>();
        var menu = new ApplicationMenuItem(SubscriptionPublicMenus.Group, l["Menu:Subscriptions"],
            icon: "fa fa-layer-group");
        menu.AddItem(new ApplicationMenuItem(SubscriptionPublicMenus.Plans, l["Plans"], "/subscriptions/plans"));
        menu.AddItem(new ApplicationMenuItem(SubscriptionPublicMenus.Bundles, l["Bundles"], "/subscriptions/bundles"));
        if (context.ServiceProvider.GetRequiredService<ICurrentUser>().IsAuthenticated)
        {
            menu.AddItem(new ApplicationMenuItem(SubscriptionPublicMenus.Mine, l["Mine"], "/subscriptions/mine"));
        }

        context.Menu.AddItem(menu);
        return Task.CompletedTask;
    }
}
