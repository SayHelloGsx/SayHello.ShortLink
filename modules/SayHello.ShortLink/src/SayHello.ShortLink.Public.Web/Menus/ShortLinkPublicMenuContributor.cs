using System.Threading.Tasks;
using SayHello.ShortLink.Localization;
using SayHello.ShortLink.Permissions;
using Volo.Abp.UI.Navigation;

namespace SayHello.ShortLink.Public.Web.Menus;

public class ShortLinkPublicMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
    }

    private Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        return ConfigureMainMenuInternalAsync(context);
    }

    private static async Task ConfigureMainMenuInternalAsync(MenuConfigurationContext context)
    {
        var localizer = context.GetLocalizer<ShortLinkResource>();

        if (await context.IsGrantedAsync(ShortLinkPublicPermissions.Default))
        {
            context.Menu.AddItem(
                new ApplicationMenuItem(
                    ShortLinkPublicMenus.MyLinks,
                    localizer["Menu:MyShortLinks"],
                    "/short-links",
                    "fa fa-link"));
        }
    }
}
