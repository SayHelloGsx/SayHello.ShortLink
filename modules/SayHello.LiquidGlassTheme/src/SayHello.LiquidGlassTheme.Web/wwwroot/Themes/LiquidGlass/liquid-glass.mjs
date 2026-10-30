export function canUseWebGl({ enabled, reducedMotion, forcedColors }) {
    return Boolean(enabled && !reducedMotion && !forcedColors);
}

export function glassConfig(radius, kind = "panel", theme = "light") {
    const control = kind === "control";
    const navigation = kind === "navigation";
    return {
        cornerRadius: Number.isFinite(radius) ? Math.max(0, radius) : 24,
        zRadius: control || navigation ? 12 : 20,
        refraction: navigation ? .6 : .72,
        blurAmount: control || navigation ? .11 : .16,
        chromAberration: control ? .025 : .018,
        edgeHighlight: navigation ? .055 : .075,
        specular: control ? .065 : .035,
        fresnel: .8,
        brightness: theme === "dark" ? -.08 : .015,
        saturation: .03,
        shadowOpacity: navigation ? .025 : control ? .055 : .045,
        shadowSpread: control || navigation ? 6 : 10,
        button: control
    };
}

export function createHoverTransition({ reveal, conceal, shouldFade, setTimer = setTimeout, clearTimer = clearTimeout, delay = 160 }) {
    let target = null;
    let lastTarget = null;
    let timer = null;
    let revision = 0;
    let destroyed = false;

    function cancel() {
        revision++;
        if (timer !== null) clearTimer(timer);
        timer = null;
    }
    function hide() {
        cancel();
        target = null;
        conceal();
    }
    function activate(next) {
        if (destroyed) return;
        if (!next) return hide();
        if (target === next) return;
        cancel();
        const fade = lastTarget !== next && shouldFade();
        conceal();
        target = next;
        const currentRevision = revision;
        const show = () => {
            if (destroyed || revision !== currentRevision || target !== next) return;
            timer = null;
            reveal(next);
            lastTarget = next;
        };
        if (fade) timer = setTimer(show, delay);
        else show();
    }
    return {
        activate,
        hide,
        get target() { return target; },
        get pending() { return timer !== null; },
        destroy() { if (!destroyed) { hide(); destroyed = true; } }
    };
}

export function createLiquidGlassTheme({ theme = "light" } = {}) {
    const root = document.documentElement;
    const scene = document.querySelector("[data-lgt-scene]");
    const background = document.querySelector("[data-lgt-scene-image]");
    if (!scene || !background) return { ready: Promise.resolve(false), refresh() {}, setTheme() {}, destroy() {} };

    const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)");
    const forcedColors = matchMedia("(forced-colors: active)");
    const mobile = matchMedia("(max-width: 1100px)");
    const panelSelector = "[data-lgt-glass], .lgt-glass, .lgt-main .card, .lgt-empty-main .card";
    const controlSelector = "[data-lgt-control], [data-lgt-nav-target], .lgt-icon-button, .lgt-user-button, .btn, .form-control, .form-select";
    const overlaySelector = "dialog, .modal, .offcanvas, .lgt-menu-popup, .dropdown-menu, .select2-dropdown";
    const limit = Math.min(64, Math.max(1, Math.trunc(Number(root.dataset.lgtMaxSurfaces)) || 24));
    const navigations = new Map();
    const removers = [];
    const observed = new Set();
    let renderer = null;
    let initializing = null;
    let generation = 0;
    let failed = false;
    let destroyed = false;
    let suspended = false;
    let needsScan = true;
    let frame = 0;
    let motionFrame = 0;
    let motionDeadline = 0;
    let lastControl = null;

    function listen(target, type, handler, options) {
        target.addEventListener(type, handler, options);
        removers.push(() => target.removeEventListener(type, handler, options));
    }
    function toggleClass(element, name, enabled) {
        if (element.classList.contains(name) !== enabled) element.classList.toggle(name, enabled);
    }
    function visible(element) {
        return Boolean(element?.isConnected && element.getClientRects().length && element.getBoundingClientRect().width > 0 && element.getBoundingClientRect().height > 0);
    }
    function surface(className = "") {
        const element = document.createElement("div");
        element.className = "lgt-glass-surface " + className;
        element.dataset.lgtGenerated = "true";
        scene.append(element);
        return element;
    }

    // A fixed proxy pool keeps one renderer alive while ABP widgets add or replace their DOM.
    const slots = Array.from({ length: limit }, () => {
        const element = surface();
        element.hidden = true;
        return { surface: element, target: null, kind: "panel" };
    });
    const hover = surface("lgt-control-surface");
    hover.style.zIndex = "4";

    function setBox(element, target) {
        if (!visible(target)) {
            if (!element.hidden) element.hidden = true;
            return;
        }
        const bounds = target.getBoundingClientRect();
        const origin = scene.getBoundingClientRect();
        if (element.hidden) element.hidden = false;
        const values = {
            left: `${(bounds.left - origin.left).toFixed(2)}px`,
            top: `${(bounds.top - origin.top).toFixed(2)}px`,
            width: `${bounds.width.toFixed(2)}px`,
            height: `${bounds.height.toFixed(2)}px`
        };
        for (const [property, value] of Object.entries(values)) {
            if (element.style[property] !== value) element.style[property] = value;
        }
    }
    function configure(element, target, kind) {
        const radius = target ? parseFloat(getComputedStyle(target).borderTopLeftRadius) : 12;
        const value = JSON.stringify(glassConfig(radius, kind, theme));
        if (element.dataset.config !== value) element.dataset.config = value;
        element.style.borderRadius = `${Number.isFinite(radius) ? radius : 24}px`;
        element.style.zIndex = kind === "navigation" ? "3" : kind === "control" ? "4" : "1";
    }
    function markReady(target, ready) {
        if (!target) return;
        if (ready) target.dataset.lgtGlassReady = "true";
        else delete target.dataset.lgtGlassReady;
    }
    function forward(type) {
        if (renderer) hover.dispatchEvent(new PointerEvent(type, { pointerId: 1, pointerType: "mouse" }));
    }
    const hoverTransition = createHoverTransition({
        reveal(control) {
            if (!visible(control) || reducedMotion.matches || forcedColors.matches || suspended) return;
            configure(hover, control, "control");
            setBox(hover, control);
            lastControl = control;
            toggleClass(hover, "is-visible", true);
            forward("pointerover");
        },
        conceal() {
            if (lastControl) forward("pointerout");
            toggleClass(hover, "is-visible", false);
        },
        shouldFade: () => !reducedMotion.matches && !forcedColors.matches && parseFloat(getComputedStyle(hover).opacity) > .02
    });

    function navigationFor(node) {
        return node instanceof Element ? navigations.get(node.closest("[data-lgt-navigation]")) : null;
    }
    function controlFor(node) {
        const control = node instanceof Element ? node.closest(controlSelector) : null;
        return control && !control.matches(":disabled, [aria-disabled='true'], [type='hidden']") &&
            !control.closest(overlaySelector) && visible(control) ? control : null;
    }
    function navigationTarget(navigation) {
        const targets = [...navigation.element.querySelectorAll("[data-lgt-nav-target]")].filter(visible);
        const focused = document.activeElement instanceof Element ? document.activeElement.closest("[data-lgt-nav-target]") : null;
        if (targets.includes(navigation.hover)) return navigation.hover;
        if (targets.includes(focused) && focused.matches(":focus-visible")) return focused;
        const active = targets.find(target => target.dataset.lgtActive === "true" || target.getAttribute("aria-current") === "page");
        if (active) return active;
        const routeTargets = targets.filter(target => {
            if (!target.href) return false;
            const url = new URL(target.href, document.baseURI);
            return url.origin === location.origin && (url.pathname === location.pathname ||
                url.pathname !== "/" && location.pathname.startsWith(url.pathname.replace(/\/$/, "") + "/"));
        }).sort((a, b) => new URL(b.href).pathname.length - new URL(a.href).pathname.length);
        return routeTargets[0] || targets[0];
    }
    function trackMotion() {
        if (destroyed || suspended || reducedMotion.matches || forcedColors.matches) return;
        motionDeadline = performance.now() + 460;
        if (motionFrame) return;
        const tick = now => {
            motionFrame = 0;
            if (destroyed || suspended) return;
            for (const slot of slots) if (slot.kind === "navigation" && slot.target) setBox(slot.surface, slot.target);
            if (now < motionDeadline) motionFrame = requestAnimationFrame(tick);
        };
        motionFrame = requestAnimationFrame(tick);
    }
    function morph(navigation, pressed = false) {
        if (reducedMotion.matches || forcedColors.matches || !navigation.indicator.animate) return;
        const current = getComputedStyle(navigation.indicator).scale;
        navigation.motion?.cancel();
        navigation.motion = navigation.indicator.animate(pressed ? [
            { scale: current === "none" ? "1" : current },
            { scale: ".98 .99", offset: .25 },
            { scale: "1" }
        ] : [
            { scale: current === "none" ? "1" : current },
            { scale: "1.045 .975", offset: .3 },
            { scale: ".995 1.005", offset: .78 },
            { scale: "1" }
        ], { duration: pressed ? 280 : 420, easing: "cubic-bezier(.22,.8,.25,1)" });
        trackMotion();
    }
    function syncNavigation(navigation, target = navigationTarget(navigation), snap = false) {
        if (!visible(target)) {
            navigation.indicator.hidden = true;
            return;
        }
        navigation.indicator.hidden = false;
        const bounds = target.getBoundingClientRect();
        const origin = navigation.element.getBoundingClientRect();
        const values = {
            "--lgt-nav-x": `${(bounds.left - origin.left - navigation.element.clientLeft + navigation.element.scrollLeft).toFixed(2)}px`,
            "--lgt-nav-y": `${(bounds.top - origin.top - navigation.element.clientTop + navigation.element.scrollTop).toFixed(2)}px`,
            "--lgt-nav-width": `${bounds.width.toFixed(2)}px`,
            "--lgt-nav-height": `${bounds.height.toFixed(2)}px`,
            "--lgt-nav-radius": getComputedStyle(target).borderTopLeftRadius
        };
        const animated = !snap && !reducedMotion.matches && !forcedColors.matches;
        toggleClass(navigation.indicator, "is-animated", animated);
        let changed = navigation.target !== target;
        for (const [property, value] of Object.entries(values)) {
            if (navigation.indicator.style.getPropertyValue(property) !== value) {
                navigation.indicator.style.setProperty(property, value);
                changed = true;
            }
        }
        if (navigation.target && navigation.target !== target && animated) morph(navigation);
        navigation.target = target;
        navigation.indicator.dataset.target = target.closest("[data-lgt-menu-item]")?.dataset.lgtMenuItem || "";
        if (changed && animated) trackMotion();
    }
    function scan() {
        needsScan = false;
        for (const [element, navigation] of navigations) {
            if (!element.isConnected) {
                navigation.motion?.cancel();
                navigation.indicator.remove();
                navigations.delete(element);
            }
        }
        for (const element of document.querySelectorAll("[data-lgt-navigation]")) {
            if (navigations.has(element)) continue;
            const indicator = document.createElement("span");
            indicator.className = "lgt-nav-indicator";
            indicator.dataset.lgtGenerated = "true";
            indicator.setAttribute("aria-hidden", "true");
            element.append(indicator);
            const navigation = { element, indicator, target: null, hover: null, motion: null };
            navigations.set(element, navigation);
            syncNavigation(navigation, undefined, true);
        }
        const panels = [...document.querySelectorAll(panelSelector)].filter(panel =>
            visible(panel) && !panel.closest(overlaySelector) && !panel.parentElement?.closest(panelSelector));
        panels.sort((a, b) => Number(b.hasAttribute("data-lgt-glass")) - Number(a.hasAttribute("data-lgt-glass")));
        const candidates = [...navigations.values()].map(navigation => navigation.indicator).concat(panels).slice(0, limit);
        for (const slot of slots) {
            if (slot.target && !candidates.includes(slot.target)) {
                markReady(slot.target, false);
                slot.target = null;
                slot.surface.hidden = true;
            }
        }
        for (const target of candidates) {
            let slot = slots.find(item => item.target === target);
            if (!slot) {
                slot = slots.find(item => !item.target);
                slot.target = target;
                slot.kind = target.classList.contains("lgt-nav-indicator") ? "navigation" : "panel";
            }
            configure(slot.surface, target, slot.kind);
            markReady(target, Boolean(renderer));
        }
        if (resizeObserver) {
            const next = new Set([document.body, ...candidates]);
            for (const navigation of navigations.values()) {
                next.add(navigation.element);
                for (const target of navigation.element.querySelectorAll("[data-lgt-nav-target]")) next.add(target);
            }
            for (const element of observed) if (!next.has(element)) { resizeObserver.unobserve(element); observed.delete(element); }
            for (const element of next) if (!observed.has(element)) { resizeObserver.observe(element); observed.add(element); }
        }
    }
    function refresh() {
        if (destroyed || suspended || document.hidden || frame) return;
        frame = requestAnimationFrame(() => {
            frame = 0;
            if (needsScan) scan();
            for (const navigation of navigations.values()) syncNavigation(navigation);
            for (const slot of slots) if (slot.target) setBox(slot.surface, slot.target);
            if (visible(hoverTransition.target)) {
                if (!hoverTransition.pending) setBox(hover, hoverTransition.target);
            } else hoverTransition.hide();
        });
    }
    function setTheme(next) {
        theme = next === "dark" ? "dark" : "light";
        for (const slot of slots) configure(slot.surface, slot.target, slot.kind);
        configure(hover, hoverTransition.target, "control");
        renderer?.markChanged();
        refresh();
    }
    function resetMotion() {
        hoverTransition.hide();
        cancelAnimationFrame(motionFrame);
        motionFrame = 0;
        for (const navigation of navigations.values()) {
            navigation.hover = null;
            navigation.motion?.cancel();
            navigation.motion = null;
            syncNavigation(navigation, undefined, true);
        }
    }
    function eligible() {
        return !destroyed && !suspended && !document.hidden && !failed && canUseWebGl({
            enabled: root.dataset.lgtWebgl === "true",
            reducedMotion: reducedMotion.matches,
            forcedColors: forcedColors.matches
        });
    }
    function stopRenderer(status = "css") {
        generation++;
        renderer?.destroy();
        renderer = null;
        for (const slot of slots) markReady(slot.target, false);
        scene.dataset.renderer = status;
    }
    async function decodeBackground(token) {
        while (!destroyed && token === generation) {
            const source = background.src;
            try { await background.decode(); }
            catch (error) { if (source === background.src) throw error; }
            if (source === background.src) return;
        }
    }
    function startRenderer() {
        if (!eligible()) {
            if (!renderer) scene.dataset.renderer = suspended || document.hidden ? "paused" : "css";
            return Promise.resolve(false);
        }
        if (renderer) return Promise.resolve(true);
        if (initializing) return initializing.then(() => renderer ? true : eligible() ? startRenderer() : false);
        const token = ++generation;
        scene.dataset.renderer = "loading";
        let pending;
        pending = (async () => {
            try {
                await (document.fonts?.ready || Promise.resolve());
                await decodeBackground(token);
                if (token !== generation || !eligible()) return false;
                const { LiquidGlass } = await import("./vendor/liquidglass.js");
                if (token !== generation || !eligible()) return false;
                for (const slot of slots) if (slot.target) setBox(slot.surface, slot.target);
                const instance = await LiquidGlass.init({ root: scene, glassElements: slots.map(slot => slot.surface).concat(hover) });
                if (token !== generation || !eligible()) { instance.destroy(); return false; }
                renderer = instance;
                scene.dataset.renderer = "webgl";
                delete scene.dataset.fallbackReason;
                for (const slot of slots) markReady(slot.target, true);
                if (lastControl && hover.classList.contains("is-visible")) forward("pointerover");
                refresh();
                return true;
            } catch (error) {
                if (!destroyed && token === generation) {
                    failed = true;
                    scene.dataset.renderer = "css";
                    scene.dataset.fallbackReason = String(error.message || error);
                }
                return false;
            } finally {
                if (initializing === pending) initializing = null;
            }
        })();
        initializing = pending;
        return pending;
    }
    function setMenuOpen(open) {
        root.dataset.lgtNavOpen = String(open);
        for (const button of document.querySelectorAll("[data-lgt-menu-toggle]")) button.setAttribute("aria-expanded", String(open));
        needsScan = true;
        refresh();
    }

    listen(document, "pointerover", event => {
        if (event.pointerType === "touch") return;
        const navigation = navigationFor(event.target);
        const control = controlFor(event.target);
        if (navigation) {
            hoverTransition.hide();
            if (control?.matches("[data-lgt-nav-target]")) { navigation.hover = control; syncNavigation(navigation, control); }
        } else hoverTransition.activate(control);
    });
    listen(document, "pointerout", event => {
        if (event.pointerType === "touch") return;
        const navigation = navigationFor(event.target);
        const nextNavigation = navigationFor(event.relatedTarget);
        const next = controlFor(event.relatedTarget);
        if (navigation && navigation === nextNavigation) {
            if (next?.matches("[data-lgt-nav-target]")) { navigation.hover = next; syncNavigation(navigation, next); }
            return;
        }
        if (navigation) { navigation.hover = null; syncNavigation(navigation); }
        if (!nextNavigation && next) hoverTransition.activate(next);
        else if (!hoverTransition.target?.matches(":focus-visible")) hoverTransition.hide();
    });
    listen(document, "pointermove", event => {
        if (event.pointerType === "touch") return;
        const control = controlFor(event.target);
        if (!control) return;
        const bounds = control.getBoundingClientRect();
        const x = Math.min(100, Math.max(0, (event.clientX - bounds.left) / bounds.width * 100));
        const y = Math.min(100, Math.max(0, (event.clientY - bounds.top) / bounds.height * 100));
        control.style.setProperty("--lgt-pointer-x", `${x.toFixed(1)}%`);
        control.style.setProperty("--lgt-pointer-y", `${y.toFixed(1)}%`);
        const navigation = navigationFor(control);
        if (navigation && control.matches("[data-lgt-nav-target]")) {
            navigation.hover = control;
            syncNavigation(navigation, control);
        } else if (!navigation) {
            hoverTransition.activate(control);
            if (!hoverTransition.pending) setBox(hover, control);
        }
    }, { passive: true });
    listen(document, "pointerdown", event => {
        const control = controlFor(event.target);
        const navigation = navigationFor(control);
        if (navigation && control?.matches("[data-lgt-nav-target]")) {
            syncNavigation(navigation, control);
            morph(navigation, true);
        } else if (control) {
            hoverTransition.activate(control);
            if (!hoverTransition.pending) forward("pointerdown");
        }
    });
    listen(document, "pointerup", event => { forward("pointerup"); if (event.pointerType === "touch") hoverTransition.hide(); });
    listen(document, "pointercancel", () => { forward("pointercancel"); hoverTransition.hide(); });
    listen(document, "focusin", event => {
        const control = controlFor(event.target);
        const navigation = navigationFor(control);
        if (navigation && control?.matches("[data-lgt-nav-target]")) {
            hoverTransition.hide();
            navigation.hover = null;
            syncNavigation(navigation, control);
        } else hoverTransition.activate(control);
    });
    listen(document, "focusout", () => {
        hoverTransition.hide();
        queueMicrotask(() => { if (!destroyed) for (const navigation of navigations.values()) syncNavigation(navigation); });
    });
    listen(document, "click", event => {
        if (!(event.target instanceof Element)) return;
        const toggle = event.target.closest("[data-lgt-menu-toggle]");
        if (toggle) setMenuOpen(root.dataset.lgtNavOpen !== "true");
        else if (mobile.matches && root.dataset.lgtNavOpen === "true" && !event.target.closest("[data-lgt-header]")) setMenuOpen(false);
        for (const details of document.querySelectorAll(".lgt-menu-group[open], .lgt-toolbar-menu[open]")) {
            if (!details.contains(event.target)) details.open = false;
        }
    });
    listen(document, "keydown", event => {
        if (event.key === "Tab") for (const navigation of navigations.values()) navigation.hover = null;
        if (event.key !== "Escape" || !(event.target instanceof Element)) return;
        const details = event.target.closest(".lgt-menu-group[open], .lgt-toolbar-menu[open]");
        if (details) {
            details.open = false;
            details.querySelector(":scope > summary")?.focus();
            event.preventDefault();
        } else if (root.dataset.lgtNavOpen === "true" && event.target.closest("[data-lgt-header]")) {
            setMenuOpen(false);
            document.querySelector("[data-lgt-menu-toggle]")?.focus();
            event.preventDefault();
        }
    });
    listen(window, "blur", resetMotion);
    listen(window, "resize", () => { needsScan = true; refresh(); }, { passive: true });
    listen(document, "scroll", refresh, { passive: true, capture: true });
    listen(background, "load", () => { renderer?.markChanged(background); refresh(); });
    listen(mobile, "change", () => { setMenuOpen(false); resetMotion(); });
    const accessibilityChanged = () => {
        resetMotion();
        if (!eligible()) stopRenderer();
        else void startRenderer();
    };
    listen(reducedMotion, "change", accessibilityChanged);
    listen(forcedColors, "change", accessibilityChanged);
    listen(document, "visibilitychange", () => {
        if (document.hidden) {
            cancelAnimationFrame(frame);
            frame = 0;
            resetMotion();
            stopRenderer("paused");
        } else { needsScan = true; refresh(); void startRenderer(); }
    });
    listen(window, "pagehide", event => {
        if (!event.persisted) return destroy();
        suspended = true;
        cancelAnimationFrame(frame);
        frame = 0;
        resetMotion();
        stopRenderer("paused");
    });
    listen(window, "pageshow", event => {
        if (event.persisted) { suspended = false; needsScan = true; refresh(); void startRenderer(); }
    });
    const resizeObserver = typeof ResizeObserver === "function" ? new ResizeObserver(refresh) : null;
    const mutationObserver = typeof MutationObserver === "function" ? new MutationObserver(records => {
        if (!records.some(record => !(record.target instanceof Element) || !record.target.closest("[data-lgt-generated], [data-lgt-scene]"))) return;
        needsScan = true;
        refresh();
    }) : null;
    mutationObserver?.observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ["hidden", "open", "class", "aria-current", "data-lgt-active"] });

    function destroy() {
        if (destroyed) return;
        destroyed = true;
        cancelAnimationFrame(frame);
        cancelAnimationFrame(motionFrame);
        hoverTransition.destroy();
        resizeObserver?.disconnect();
        mutationObserver?.disconnect();
        removers.forEach(remove => remove());
        stopRenderer();
        for (const navigation of navigations.values()) { navigation.motion?.cancel(); navigation.indicator.remove(); }
        navigations.clear();
        for (const slot of slots) slot.surface.remove();
        hover.remove();
        delete root.dataset.lgtEnhanced;
        delete root.dataset.lgtNavOpen;
        for (const button of document.querySelectorAll("[data-lgt-menu-toggle]")) button.setAttribute("aria-expanded", "false");
    }

    root.dataset.lgtEnhanced = "true";
    setMenuOpen(false);
    scan();
    setTheme(theme);
    const ready = startRenderer();
    return { ready, refresh, setTheme, destroy };
}
