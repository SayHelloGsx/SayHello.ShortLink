(function () {
    "use strict";

    function boot() {
        var root = document.documentElement;
        if (!root.dataset.lgtModuleUrl || window.liquidGlassTheme) return;
        var controller = null;
        var disposed = false;
        var tenantModal = null;
        var removers = [];
        var storageKey = "SayHello.LiquidGlass.ColorMode";
        var modes = ["system", "light", "dark"];
        var defaultMode = modes.indexOf(root.dataset.lgtDefaultMode) >= 0 ? root.dataset.lgtDefaultMode : "system";
        var mode = modes.indexOf(root.dataset.lgtMode) >= 0 ? root.dataset.lgtMode : defaultMode;
        var systemColor = window.matchMedia("(prefers-color-scheme: dark)");

        function listen(target, type, handler) {
            target.addEventListener(type, handler);
            removers.push(function () { target.removeEventListener(type, handler); });
        }

        function applyMode(next, persist) {
            if (disposed || modes.indexOf(next) < 0) return;
            mode = next;
            var theme = mode === "dark" || (mode === "system" && systemColor.matches) ? "dark" : "light";
            root.dataset.lgtMode = mode;
            root.dataset.lgtTheme = theme;
            root.dataset.bsTheme = theme;
            document.querySelectorAll("[data-lgt-mode]").forEach(function (button) {
                button.setAttribute("aria-pressed", String(button.dataset.lgtMode === mode));
            });
            var background = document.querySelector("[data-lgt-scene-image]");
            if (background) {
                var source = theme === "dark" ? background.dataset.lgtDarkSrc : background.dataset.lgtLightSrc;
                if (source && background.getAttribute("src") !== source) background.src = source;
            }
            if (persist) {
                try { localStorage.setItem(storageKey, mode); } catch (_) { }
            }
            if (controller) controller.setTheme(theme);
            document.dispatchEvent(new CustomEvent("liquidglass:themechange", { detail: { mode: mode, theme: theme } }));
        }

        listen(document, "click", function (event) {
            if (!(event.target instanceof Element)) return;
            var modeButton = event.target.closest("[data-lgt-mode]");
            if (modeButton) applyMode(modeButton.dataset.lgtMode, true);
            var tenantButton = event.target.closest("[data-lgt-tenant-switch]");
            if (tenantButton && window.abp && window.abp.ModalManager) {
                if (!tenantModal) {
                    tenantModal = new window.abp.ModalManager({ viewUrl: window.abp.appPath + "Abp/MultiTenancy/TenantSwitchModal" });
                    tenantModal.onResult(function () { window.location.reload(); });
                }
                tenantModal.open();
            }
        });
        listen(systemColor, "change", function () { if (mode === "system") applyMode(mode, false); });
        listen(window, "storage", function (event) {
            if (event.key === storageKey || event.key === null) {
                applyMode(modes.indexOf(event.newValue) >= 0 ? event.newValue : defaultMode, false);
            }
        });
        listen(window, "pageshow", function (event) {
            if (event.persisted) {
                applyMode(mode, false);
                if (controller) controller.refresh();
            }
        });

        function destroy() {
            if (disposed) return;
            disposed = true;
            removers.forEach(function (remove) { remove(); });
            if (controller) controller.destroy();
        }
        listen(window, "pagehide", function (event) { if (!event.persisted) destroy(); });
        applyMode(mode, false);

        var ready = import(new URL(root.dataset.lgtModuleUrl, document.baseURI).href)
            .then(function (module) {
                if (disposed) return false;
                controller = module.createLiquidGlassTheme({ theme: root.dataset.lgtTheme });
                return controller.ready;
            })
            .catch(function (error) {
                var scene = document.querySelector("[data-lgt-scene]");
                if (scene) {
                    scene.dataset.renderer = "css";
                    scene.dataset.fallbackReason = String(error.message || error);
                }
                return false;
            });
        window.liquidGlassTheme = {
            get mode() { return mode; },
            setMode: function (next) { applyMode(next, true); },
            refresh: function () { if (controller) controller.refresh(); },
            destroy: destroy,
            ready: ready
        };
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", boot, { once: true });
    else boot();
}());
