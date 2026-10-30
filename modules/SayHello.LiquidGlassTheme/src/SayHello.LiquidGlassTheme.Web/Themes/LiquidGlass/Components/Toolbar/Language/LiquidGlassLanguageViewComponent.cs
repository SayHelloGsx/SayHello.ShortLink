using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;

namespace SayHello.LiquidGlassTheme.Components.Toolbar.Language;

public class LiquidGlassLanguageViewComponent : AbpViewComponent
{
    protected ILanguageProvider LanguageProvider { get; }

    public LiquidGlassLanguageViewComponent(ILanguageProvider languageProvider)
    {
        LanguageProvider = languageProvider;
    }

    public virtual async Task<IViewComponentResult> InvokeAsync()
    {
        var languages = await LanguageProvider.GetLanguagesAsync();
        if (languages.Count < 2)
        {
            return Content(string.Empty);
        }

        var current = languages.FirstOrDefault(language =>
            language.UiCultureName == CultureInfo.CurrentUICulture.Name) ??
            languages.FirstOrDefault(language => language.CultureName == CultureInfo.CurrentCulture.Name) ??
            languages[0];

        return View("~/Themes/LiquidGlass/Components/Toolbar/Language/Default.cshtml",
            new LiquidGlassLanguageViewModel { Languages = languages, CurrentLanguage = current });
    }
}
