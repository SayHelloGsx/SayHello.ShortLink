import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath, pathToFileURL } from "node:url";
import vm from "node:vm";

const assets = path.join(path.dirname(fileURLToPath(import.meta.url)), "..", "src", "SayHello.LiquidGlassTheme.Web", "wwwroot", "Themes", "LiquidGlass");
const { canUseWebGl, glassConfig, createHoverTransition, createLiquidGlassTheme } = await import(pathToFileURL(path.join(assets, "liquid-glass.mjs")));
const { resolveFontFaceUrls } = await import(pathToFileURL(path.join(assets, "liquid-glass-fonts.mjs")));
const initialization = readFileSync(path.join(assets, "liquid-glass-init.js"), "utf8");
const localization = path.join(assets, "..", "..", "..", "Localization", "LiquidGlassTheme");

for (const culture of ["en", "zh-Hans"]) {
    test(`Localization resource is valid and complete: ${culture}`, () => {
        const resource = JSON.parse(readFileSync(path.join(localization, `${culture}.json`), "utf8"));
        const baseline = JSON.parse(readFileSync(path.join(localization, "en.json"), "utf8"));
        assert.equal(resource.culture, culture);
        assert.deepEqual(Object.keys(resource.texts).sort(), Object.keys(baseline.texts).sort());
        for (const [key, value] of Object.entries(resource.texts)) {
            assert.equal(typeof value, "string", `${key} must be a string`);
            assert.ok(value.trim(), `${key} must not be empty`);
        }
    });
}

for (const [name, css, stylesheet, expected] of [
    ["ABP bundle-relative fonts", 'src:url(../libs/icons.woff2)', 'https://localhost/__bundles/theme.css', 'src:url("https://localhost/libs/icons.woff2")'],
    ["application path bases", "src:url('../libs/icons.woff2?v=2')", 'https://localhost/app/__bundles/theme.css', 'src:url("https://localhost/app/libs/icons.woff2?v=2")'],
    ["root paths and quoted spaces", 'src:local("Icons"),url("/fonts/icon font.woff2")', 'https://localhost/admin/style.css', 'src:local("Icons"),url("https://localhost/fonts/icon%20font.woff2")'],
    ["data and external sources", 'src:url(data:font/woff2;base64,AA==),url(https://fonts.example.test/icon.woff2)', 'https://localhost/theme.css', 'src:url("data:font/woff2;base64,AA=="),url("https://fonts.example.test/icon.woff2")'],
    ["invalid base URL", 'src:url(../fonts/icon.woff2)', 'not-a-url', 'src:url(../fonts/icon.woff2)'],
    ["empty URLs and local fonts", 'src:local("Icons"),url("")', 'https://localhost/theme.css', 'src:local("Icons"),url("")']
]) {
    test(`Font sources resolve against their stylesheet: ${name}`, () => {
        assert.equal(resolveFontFaceUrls(css, stylesheet), expected);
    });
}

function initialize({ defaultMode = "system", saved = null, dark = false, denied = false } = {}) {
    const root = { dataset: { lgtDefaultMode: defaultMode } };
    vm.runInNewContext(initialization, {
        document: { documentElement: root },
        window: { matchMedia: query => { assert.equal(query, "(prefers-color-scheme: dark)"); return { matches: dark }; } },
        localStorage: { getItem: key => {
            assert.equal(key, "SayHello.LiquidGlass.ColorMode");
            if (denied) throw new Error("Storage unavailable");
            return saved;
        } }
    });
    return root.dataset;
}

for (const [defaultMode, saved, dark, expectedMode, expectedTheme] of [
    ["system", null, false, "system", "light"],
    ["system", null, true, "system", "dark"],
    ["light", null, true, "light", "light"],
    ["dark", null, false, "dark", "dark"],
    ["light", "dark", false, "dark", "dark"],
    ["dark", "light", true, "light", "light"],
    ["light", "system", true, "system", "dark"],
    ["invalid", "invalid", true, "system", "dark"],
    ["light", "invalid", true, "light", "light"]
]) {
    test(`Initial color mode: ${defaultMode}, saved ${saved}, system dark ${dark}`, () => {
        const result = initialize({ defaultMode, saved, dark });
        assert.equal(result.lgtMode, expectedMode);
        assert.equal(result.lgtTheme, expectedTheme);
        assert.equal(result.bsTheme, expectedTheme);
    });
}

test("Unavailable local storage preserves the configured/system fallback", () => {
    assert.equal(initialize({ denied: true, dark: true }).lgtTheme, "dark");
    assert.equal(initialize({ defaultMode: "light", denied: true, dark: true }).lgtTheme, "light");
});

test("WebGL respects all configured, reduced-motion and forced-colors combinations", () => {
    for (const enabled of [false, true]) {
        for (const reducedMotion of [false, true]) {
            for (const forcedColors of [false, true]) {
                assert.equal(canUseWebGl({ enabled, reducedMotion, forcedColors }), enabled && !reducedMotion && !forcedColors);
            }
        }
    }
});

test("Glass configurations distinguish panels, navigation and controls", () => {
    const panel = glassConfig(28);
    const navigation = glassConfig(16, "navigation");
    const control = glassConfig(12, "control", "dark");
    assert.equal(panel.cornerRadius, 28);
    assert.equal(panel.button, false);
    assert.equal(navigation.button, false);
    assert.equal(control.button, true);
    assert.ok(navigation.refraction < panel.refraction);
    assert.ok(control.blurAmount < panel.blurAmount);
    assert.ok(control.brightness < panel.brightness);
    assert.ok(control.shadowOpacity > navigation.shadowOpacity);
    for (const config of [panel, navigation, control]) {
        for (const [key, value] of Object.entries(config)) {
            if (key !== "button") assert.ok(Number.isFinite(value), `${key} must be finite`);
        }
    }
});

test("Invalid radii use a safe default, and negative radii are clamped", () => {
    for (const radius of [undefined, null, NaN, Infinity, "28px"]) assert.equal(glassConfig(radius).cornerRadius, 24);
    assert.equal(glassConfig(-10).cornerRadius, 0);
    assert.equal(glassConfig(0).cornerRadius, 0);
});

function hoverFixture() {
    const timers = new Map();
    const revealed = [];
    let counter = 0;
    let fading = false;
    let concealed = 0;
    const transition = createHoverTransition({
        reveal: target => revealed.push(target),
        conceal: () => concealed++,
        shouldFade: () => fading,
        setTimer: (callback, delay) => {
            const id = ++counter;
            assert.equal(delay, 160);
            timers.set(id, callback);
            return id;
        },
        clearTimer: id => timers.delete(id)
    });
    return {
        transition, timers, revealed,
        get concealed() { return concealed; },
        set fading(value) { fading = value; },
        flush() {
            for (const [id, callback] of [...timers]) { timers.delete(id); callback(); }
        }
    };
}

test("The first hover reveals immediately without a timer", () => {
    const fixture = hoverFixture();
    fixture.transition.activate("first");
    assert.deepEqual(fixture.revealed, ["first"]);
    assert.equal(fixture.transition.target, "first");
    assert.equal(fixture.transition.pending, false);
    assert.equal(fixture.timers.size, 0);
});

test("Moving between controls fades the old location before revealing the new one", () => {
    const fixture = hoverFixture();
    fixture.transition.activate("first");
    fixture.fading = true;
    fixture.transition.activate("second");
    assert.deepEqual(fixture.revealed, ["first"]);
    assert.equal(fixture.transition.pending, true);
    assert.equal(fixture.concealed, 2);
    fixture.flush();
    assert.deepEqual(fixture.revealed, ["first", "second"]);
    assert.equal(fixture.transition.pending, false);
});

test("Leaving before the delayed reveal cancels the timer", () => {
    const fixture = hoverFixture();
    fixture.transition.activate("first");
    fixture.fading = true;
    fixture.transition.activate("second");
    fixture.transition.hide();
    fixture.flush();
    assert.deepEqual(fixture.revealed, ["first"]);
    assert.equal(fixture.transition.target, null);
    assert.equal(fixture.transition.pending, false);
    assert.equal(fixture.timers.size, 0);
});

test("Rapid hover changes cannot execute a stale reveal or clear the current timer", () => {
    const fixture = hoverFixture();
    fixture.transition.activate("first");
    fixture.fading = true;
    fixture.transition.activate("second");
    const stale = [...fixture.timers.values()][0];
    fixture.transition.activate("third");
    stale();
    assert.equal(fixture.transition.pending, true);
    assert.equal(fixture.timers.size, 1);
    fixture.flush();
    assert.deepEqual(fixture.revealed, ["first", "third"]);
});

test("Reentering the same pending target does not restart its timer", () => {
    const fixture = hoverFixture();
    fixture.transition.activate("first");
    fixture.fading = true;
    fixture.transition.activate("second");
    const id = [...fixture.timers.keys()][0];
    fixture.transition.activate("second");
    assert.deepEqual([...fixture.timers.keys()], [id]);
});

test("Returning to the last visible control reveals immediately", () => {
    const fixture = hoverFixture();
    fixture.transition.activate("first");
    fixture.fading = true;
    fixture.transition.activate("second");
    fixture.transition.activate("first");
    assert.deepEqual(fixture.revealed, ["first", "first"]);
    assert.equal(fixture.timers.size, 0);
});

test("Destroy is idempotent and prevents pending/future reveals", () => {
    const fixture = hoverFixture();
    fixture.transition.activate("first");
    fixture.fading = true;
    fixture.transition.activate("second");
    const stale = [...fixture.timers.values()][0];
    fixture.transition.destroy();
    const concealed = fixture.concealed;
    fixture.transition.destroy();
    fixture.transition.activate("third");
    stale();
    fixture.flush();
    assert.deepEqual(fixture.revealed, ["first"]);
    assert.equal(fixture.timers.size, 0);
    assert.equal(fixture.concealed, concealed);
});

test("Missing scene markup provides a harmless controller", async () => {
    const previous = globalThis.document;
    globalThis.document = { documentElement: {}, querySelector: () => null };
    try {
        const controller = createLiquidGlassTheme();
        assert.equal(await controller.ready, false);
        controller.refresh();
        controller.setTheme("dark");
        controller.destroy();
        controller.destroy();
    } finally {
        if (previous === undefined) delete globalThis.document;
        else globalThis.document = previous;
    }
});
