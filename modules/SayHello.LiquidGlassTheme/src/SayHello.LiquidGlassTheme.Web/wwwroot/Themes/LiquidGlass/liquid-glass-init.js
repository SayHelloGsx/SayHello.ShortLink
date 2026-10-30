(function () {
    "use strict";
    var root = document.documentElement;
    var modes = ["system", "light", "dark"];
    var mode = root.dataset.lgtDefaultMode;
    if (modes.indexOf(mode) < 0) mode = "system";
    try {
        var saved = localStorage.getItem("SayHello.LiquidGlass.ColorMode");
        if (modes.indexOf(saved) >= 0) mode = saved;
    } catch (_) {
        // Storage can be unavailable in private or embedded browsing contexts.
    }
    var dark = mode === "dark" || (mode === "system" && window.matchMedia("(prefers-color-scheme: dark)").matches);
    root.dataset.lgtMode = mode;
    root.dataset.lgtTheme = dark ? "dark" : "light";
    root.dataset.bsTheme = root.dataset.lgtTheme;
}());
