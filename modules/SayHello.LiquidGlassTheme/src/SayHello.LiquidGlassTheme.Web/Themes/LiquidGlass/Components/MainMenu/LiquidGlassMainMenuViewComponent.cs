using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Layout;
using Volo.Abp.UI.Navigation;

namespace SayHello.LiquidGlassTheme.Components.MainMenu;

public class LiquidGlassMainMenuViewComponent : AbpViewComponent
{
    protected IMenuManager MenuManager { get; }
    protected IPageLayout PageLayout { get; }

    public LiquidGlassMainMenuViewComponent(IMenuManager menuManager, IPageLayout pageLayout)
    {
        MenuManager = menuManager;
        PageLayout = pageLayout;
    }

    public virtual async Task<IViewComponentResult> InvokeAsync()
    {
        var menu = await MenuManager.GetMainMenuAsync();
        var models = menu.Items.OrderBy(item => item.Order).Select(item =>
            LiquidGlassMenuItemViewModel.Create(item, PageLayout.Content.MenuItemName,
                $"{Request.PathBase}{Request.Path}", url => Url.Content(url))).ToArray();

        return View("~/Themes/LiquidGlass/Components/MainMenu/Default.cshtml", models);
    }
}
