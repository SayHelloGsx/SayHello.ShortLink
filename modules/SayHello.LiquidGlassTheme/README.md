# SayHello.LiquidGlassTheme

A reusable Razor Pages / MVC theme for **ABP Framework 10.6.0** and **.NET 10**.
The ShortLink WebHost uses it as its default theme. It does not depend on ShortLink,
Subscription, Lepton, or a commercial theme package. Angular and Blazor are out of scope.

## Design and behavior

- Light, dark, and system color modes, with a synchronous first-paint initializer,
  local preference persistence, and cross-tab updates.
- Liquid-glass navigation capsules, soft hover transitions, and glass panels over
  locally served day/night backgrounds.
- Application, Account, and Empty layouts; ABP Public pages use Application by default.
- ABP branding, recursive permission-filtered menus, the user/language/appearance
  toolbar, breadcrumbs, alerts, tenant switching, and themed error pages.
- Standard ABP bundles, localization/configuration/service-proxy scripts, widget
  resources, layout hooks, antiforgery, and page `styles`, `scripts`, and
  `content_toolbar` sections are preserved.
- Bootstrap-based forms, tables, dialogs, and existing module pages remain real,
  interactive DOM content. The theme does not introduce sample business data.

The optional WebGL renderer receives only the decorative scene and empty proxy
surfaces. It never rasterizes business forms, tables, or user content. A fixed
surface pool bounds rendering work; surfaces outside the pool keep their CSS glass
appearance. Reduced motion, forced colors, disabled/unavailable WebGL, and renderer
errors use the CSS fallback. Navigation and page content remain usable without the
renderer; the base mobile navigation also remains accessible without JavaScript.

## Host integration

Reference `src\SayHello.LiquidGlassTheme.Web\SayHello.LiquidGlassTheme.Web.csproj`
from the host Web project, then replace the previous theme module in `[DependsOn]`:

```csharp
using SayHello.LiquidGlassTheme;
using Volo.Abp.AspNetCore.Mvc.UI.Theming;
using Volo.Abp.Modularity;
using GlassTheme = SayHello.LiquidGlassTheme.LiquidGlassTheme;

[DependsOn(typeof(LiquidGlassThemeWebModule))]
public class MyWebModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpThemingOptions>(options =>
            options.DefaultThemeName = GlassTheme.Name);
    }
}
```

The registered theme name is **`SayHello.LiquidGlass`**. The module selects itself
only if no default theme is already configured; explicitly select it when replacing
another theme. The alias above also avoids a C# namespace/type collision in hosts
whose namespace starts with `SayHello`.

Use `@abp/aspnetcore.mvc.ui.theme.shared` `~10.6.0` in the host `package.json`.
Retain the host's existing ABP library mappings, install its client dependencies,
and run ABP's standard library installation from the repository root:

```powershell
abp install-libs --working-directory .\host\src\SayHello.ShortLink.WebHost.Web
```

The theme's own CSS, scripts, SVGs, and renderer are embedded in the Web assembly
and served through ABP's virtual file system under `/Themes/LiquidGlass/`.
They need no CDN, separate frontend build, or CSP relaxation. Add host CSS to
`LiquidGlassThemeBundles.Styles.Global` and host scripts to
`LiquidGlassThemeBundles.Scripts.Global` rather than a removed theme's bundles.

## Packaging

The Web project is packable and includes its README and third-party `NOTICE.txt`:

```powershell
dotnet pack .\modules\SayHello.LiquidGlassTheme\src\SayHello.LiquidGlassTheme.Web\SayHello.LiquidGlassTheme.Web.csproj -c Release
```

Consumers can reference `SayHello.LiquidGlassTheme.Web` version `1.0.0` from their
own package feed instead of using a project reference. Razor views, localization,
and web assets are embedded; packing does not publish the package to a feed.

## Options and layout overrides

Configure `LiquidGlassThemeOptions` in the host, or bind the same properties under
the `LiquidGlassTheme` configuration section:

| Option | Default | Purpose |
| --- | --- | --- |
| `DefaultColorMode` | `System` | `System`, `Light`, or `Dark`; a valid stored user preference takes precedence. |
| `EnableWebGl` | `true` | Enable the optional renderer when accessibility preferences also permit it. |
| `MaxGlassSurfaces` | `24` | Maximum regular proxy surfaces; the client clamps it to 1–64 and reserves one additional hover surface. |
| `ShowFooter` | `true` | Show the Application layout footer. |
| `Layouts` | Empty | Override standard or custom layout names with Razor view paths. |

```csharp
Configure<LiquidGlassThemeOptions>(options =>
{
    options.DefaultColorMode = LiquidGlassColorMode.System;
    options.MaxGlassSurfaces = 24;
    options.Layouts[StandardLayouts.Account] = "~/Custom/Account.cshtml";
});
```

Unknown layout names fall back to Application unless the caller passes
`fallbackToDefault: false`. Keep ABP's infrastructure and page sections when supplying
custom layouts. Theme options change presentation only; they do not alter permissions,
tenancy, business rules, or aggregate concurrency contracts.

## Page extensions

Use `data-lgt-glass="panel"` with `lgt-glass` to opt a page surface into the glass
pool. Navigation surfaces take priority, followed by explicit panels and ordinary
cards; nested panels and popup/modal surfaces are not separately captured.

```html
<section class="lgt-glass p-4" data-lgt-glass="panel">
    <!-- Existing page content -->
</section>
```

Use `data-lgt-control` on an eligible control to opt into the hover treatment.
The renderer does not change the control's event handlers or form semantics.
Avoid marking overlays or nesting glass surfaces. Dynamic DOM changes are observed;
call `refresh()` after application-specific layout changes when needed.

After DOM initialization, the browser exposes:

```javascript
await window.liquidGlassTheme.ready; // true for WebGL, false for CSS fallback
window.liquidGlassTheme.setMode("dark"); // system, light, or dark
window.liquidGlassTheme.refresh();
window.liquidGlassTheme.destroy(); // idempotent final teardown
```

`mode` returns the selected preference. `liquidglass:themechange` is dispatched on
`document` with `{ mode, theme }` in `event.detail`; `theme` is the resolved light/dark
palette. The local-storage key is `SayHello.LiquidGlass.ColorMode`. Normal navigation
and page lifecycle cleanup are automatic, including visibility and bfcache handling.

## Tests

The frontend tests use Node's built-in test runner and require Node.js 20 or newer;
there are no test dependencies to install:

```powershell
node --test .\modules\SayHello.LiquidGlassTheme\tests\liquid-glass.test.mjs
```

Host tests cover theme/layout contracts, actual embedded assets and bundles,
Application and Account infrastructure, menu/return-URL behavior, error pages, and
existing ShortLink and Subscription routes:

```powershell
dotnet test .\host\test\SayHello.ShortLink.WebHost.Web.Tests\SayHello.ShortLink.WebHost.Web.Tests.csproj --filter "FullyQualifiedName~LiquidGlass|FullyQualifiedName~Index_Tests|FullyQualifiedName~SubscriptionRouteTests"
```

## Third-party resources

The renderer is pinned to **`@ybouane/liquidglass@1.0.3`**, reused from the earlier
liquid-glass demo under its declared MIT license. A small local integration patch
resolves linked stylesheet font URLs against the stylesheet rather than the current
page, keeping nested Razor routes and application path bases working. Its bundled
`html-to-image` license, source attribution, and patch description are retained in
`wwwroot\Themes\LiquidGlass\vendor\NOTICE.txt`. Keep that notice when redistributing.
The scene SVGs and theme implementation are project assets. The local LeptonTheme
reference informed ABP integration structure only; no commercial theme implementation
or assets are included.
