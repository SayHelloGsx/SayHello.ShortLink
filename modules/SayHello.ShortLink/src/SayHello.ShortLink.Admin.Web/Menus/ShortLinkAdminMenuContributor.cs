using System.Threading.Tasks;
using SayHello.ShortLink.Localization;
using SayHello.ShortLink.Permissions;
using Volo.Abp.UI.Navigation;

namespace SayHello.ShortLink.Admin.Web.Menus;

public class ShortLinkAdminMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name != StandardMenus.Main ||
            !await context.IsGrantedAsync(ShortLinkAdminPermissions.Default))
        {
            return;
        }

        var localizer = context.GetLocalizer<ShortLinkResource>();
        var administration = new ApplicationMenuItem(
            ShortLinkAdminMenus.Administration,
            localizer["Menu:ShortLinkAdministration"],
            icon: "fa fa-shield-alt");

        if (await context.IsGrantedAsync(ShortLinkAdminPermissions.ManageAllLinks))
        {
            administration.AddItem(
                new ApplicationMenuItem(
                    ShortLinkAdminMenus.AllLinks,
                    localizer["Menu:AllShortLinks"],
                    "/admin/short-links"));
        }

        if (await context.IsGrantedAsync(ShortLinkAdminPermissions.ManageBlockedDomains))
        {
            administration.AddItem(
                new ApplicationMenuItem(
                    ShortLinkAdminMenus.BlockedDomains,
                    localizer["Menu:BlockedDomains"],
                    "/admin/short-links/blocked-domains"));
        }

        if (await context.IsGrantedAsync(ShortLinkAdminPermissions.ManageSettings))
        {
            administration.AddItem(
                new ApplicationMenuItem(
                    ShortLinkAdminMenus.Settings,
                    localizer["Menu:ShortLinkSettings"],
                    "/admin/short-links/settings"));
        }

        if (administration.Items.Count > 0)
        {
            context.Menu.AddItem(administration);
        }
    }
}
