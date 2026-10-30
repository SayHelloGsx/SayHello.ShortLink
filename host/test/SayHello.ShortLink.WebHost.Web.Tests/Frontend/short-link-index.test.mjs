import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const sourcePath = process.env.SHORT_LINK_BUNDLE_PATH ?? path.join(
    path.dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "..", "modules", "SayHello.ShortLink",
    "src", "SayHello.ShortLink.Public.Web", "Pages", "Public", "ShortLinks", "Index.js");
const source = readFileSync(sourcePath, "utf8");

function field(properties = {}) {
    const events = new Map();
    return {
        dataset: {}, value: "", checked: false, disabled: false, hidden: true, textContent: "",
        ...properties,
        addEventListener: (name, callback) => events.set(name, callback),
        emit: name => events.get(name)?.()
    };
}

function option(value = "", unavailable = "false") {
    return { value, dataset: { unavailable }, cloneNode() { return option(this.value, this.dataset.unavailable); } };
}

function fixture({ present = true, namespaces = true, root = true, create = true, selected = "" } = {}) {
    const form = { dataset: {
        canUseNamespaces: String(namespaces), canCreateRoot: String(root), canCreate: String(create),
        namespaceListUrl: "/short-links?handler=Namespaces"
    } };
    const fields = {
        NewLink_Origin: field({ value: "https://first.example.test" }),
        NewLink_NamespaceId: field({ value: selected, options: [option(), option("owned"), option("unavailable", "true")] }),
        NewLink_UseRootPath: field(),
        "create-short-link-button": field(),
        "namespace-options-status": field(),
        "namespace-options-retry": field()
    };
    const namespace = fields.NewLink_NamespaceId;
    namespace.replaceChildren = (...options) => { namespace.options = options; namespace.value = options[0]?.value ?? ""; };
    namespace.append = value => namespace.options.push(value);
    Object.defineProperty(namespace, "selectedOptions", { get: () => namespace.options.filter(item => item.value === namespace.value) });
    const requests = [];
    let ready;
    vm.runInNewContext(source, {
        document: {
            querySelectorAll: () => [],
            getElementById(id) {
                if (id === "create-short-link-form") return present ? form : null;
                assert.ok(present, "Missing forms must not initialize their controls");
                assert.ok(id in fields, `Unexpected control: ${id}`);
                return fields[id];
            },
            createElement(tag) { assert.equal(tag, "option"); return option(); },
            addEventListener(name, callback) { assert.equal(name, "DOMContentLoaded"); ready = callback; }
        },
        abp: {
            localization: { getResource: () => key => key },
            ajax(options) {
                const request = { options };
                requests.push(request);
                return {
                    done(callback) { request.succeed = callback; return this; },
                    fail(callback) { request.fail = callback; return this; }
                };
            }
        }
    });
    assert.equal(typeof ready, "function");
    ready();
    return { fields, requests, namespace, origin: fields.NewLink_Origin, rootPath: fields.NewLink_UseRootPath,
        button: fields["create-short-link-button"], status: fields["namespace-options-status"], retry: fields["namespace-options-retry"] };
}

test("Missing create forms do not initialize namespace helpers", () => {
    assert.equal(fixture({ present: false }).requests.length, 0);
});

for (const [name, permissions, disabled] of [
    ["normal creation", {}, [false, false, false]],
    ["no namespace or root entitlement", { namespaces: false, root: false }, [true, true, false]],
    ["creation quota exhausted", { create: false }, [false, false, true]]
]) {
    test(`Initial creation state: ${name}`, () => {
        const state = fixture(permissions);
        assert.deepEqual([state.namespace.disabled, state.rootPath.disabled, state.button.disabled], disabled);
    });
}

test("Unavailable selected namespaces cannot create a link", () => {
    assert.equal(fixture({ selected: "unavailable" }).button.disabled, true);
    assert.equal(fixture({ selected: "owned", namespaces: false }).button.disabled, true);
});

test("Namespace and root-path changes remain mutually exclusive", () => {
    const state = fixture();
    state.namespace.value = "owned";
    state.rootPath.checked = true;
    state.namespace.emit("change");
    assert.equal(state.rootPath.checked, false);
    assert.equal(state.button.disabled, false);
    state.rootPath.checked = true;
    state.rootPath.emit("change");
    assert.equal(state.namespace.value, "");
    assert.equal(state.namespace.disabled, true);
    assert.equal(state.button.disabled, false);
});

test("Origin changes load namespace options and restore their controls", () => {
    const state = fixture();
    state.origin.emit("change");
    assert.equal(state.namespace.disabled, true);
    assert.equal(state.status.textContent, "Namespaces:Loading");
    assert.equal(state.requests[0].options.data.origin, state.origin.value);
    state.requests[0].succeed({ items: [{ id: "new", name: "team" }] });
    assert.equal(state.namespace.options[1].value, "new");
    assert.equal(state.namespace.options[1].textContent, "team (/team/{code})");
    assert.equal(state.namespace.disabled, false);
    assert.equal(state.status.textContent, "Namespaces:SelectHelp");
});

test("Stale namespace responses and failures cannot overwrite the current origin", () => {
    const state = fixture();
    state.origin.emit("change");
    state.origin.value = "https://second.example.test";
    state.origin.emit("change");
    state.requests[0].succeed({ items: [{ id: "stale", name: "old" }] });
    state.requests[0].fail();
    assert.equal(state.namespace.options.length, 1);
    assert.equal(state.status.textContent, "Namespaces:Loading");
    assert.equal(state.retry.hidden, true);
    state.requests[1].succeed({ items: [{ id: "current", name: "new" }] });
    assert.equal(state.namespace.options[1].value, "current");
});

test("Failed namespace lookups can be retried", () => {
    const state = fixture();
    state.origin.emit("change");
    state.requests[0].fail();
    assert.equal(state.status.textContent, "Namespaces:LoadFailed");
    assert.equal(state.retry.hidden, false);
    state.retry.emit("click");
    assert.equal(state.retry.hidden, true);
    state.requests[1].succeed({ items: [] });
    assert.equal(state.status.textContent, "Namespaces:NoneForDomain");
    assert.equal(state.namespace.disabled, false);
});

test("Empty origins and missing namespace entitlements do not issue lookups", () => {
    const state = fixture();
    state.origin.value = "";
    state.origin.emit("change");
    assert.equal(state.requests.length, 0);
    assert.equal(state.status.textContent, "Namespaces:SelectDomainHelp");
    const restricted = fixture({ namespaces: false });
    restricted.origin.emit("change");
    assert.equal(restricted.requests.length, 0);
});
