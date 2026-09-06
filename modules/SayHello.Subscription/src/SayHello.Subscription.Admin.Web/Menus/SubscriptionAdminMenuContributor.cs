using System.Threading.Tasks;
using SayHello.Subscription.Admin.Localization;
using SayHello.Subscription.Admin.Permissions;
using Volo.Abp.UI.Navigation;

namespace SayHello.Subscription.Admin.Web.Menus;

public class SubscriptionAdminMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name != StandardMenus.Main) return;
        var localizer = context.GetLocalizer<SubscriptionAdminResource>();
        var menu = new ApplicationMenuItem("Subscription.Admin", localizer["Administration"], icon: "fa fa-layer-group");
        foreach (var area in new[] { "Products", "Plans", "Bundles", "Users" })
        {
            if (await context.IsGrantedAsync(SubscriptionAdminPermissions.Default + "." + area))
                menu.AddItem(new ApplicationMenuItem("Subscription.Admin." + area,
                    localizer[area], "/admin/subscriptions/" + area.ToLowerInvariant()));
        }
        if (menu.Items.Count > 0) context.Menu.AddItem(menu);
    }
}
