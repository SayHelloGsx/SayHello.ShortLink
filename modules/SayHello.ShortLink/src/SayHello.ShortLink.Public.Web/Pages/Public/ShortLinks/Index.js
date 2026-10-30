document.addEventListener('DOMContentLoaded', () => {
    const resource = abp.localization.getResource('ShortLink');

    document.querySelectorAll('.copy-short-link').forEach(button => {
        button.addEventListener('click', async () => {
            await navigator.clipboard.writeText(button.dataset.url);
            abp.notify.success(button.dataset.copiedMessage ?? 'Copied');
        });
    });

    document.querySelectorAll('.delete-short-link-form').forEach(form => {
        form.addEventListener('submit', event => {
            if (!window.confirm(resource('DeleteConfirmation'))) {
                event.preventDefault();
            }
        });
    });

    const createForm = document.getElementById('create-short-link-form');
    if (!createForm) {
        return;
    }

    const origin = document.getElementById('NewLink_Origin');
    const namespace = document.getElementById('NewLink_NamespaceId');
    const rootPath = document.getElementById('NewLink_UseRootPath');
    const createButton = document.getElementById('create-short-link-button');
    const status = document.getElementById('namespace-options-status');
    const retry = document.getElementById('namespace-options-retry');
    const canUseNamespaces = createForm.dataset.canUseNamespaces === 'true';
    const canCreateRoot = createForm.dataset.canCreateRoot === 'true';
    const canCreate = createForm.dataset.canCreate === 'true';
    const defaultOption = namespace.options[0].cloneNode(true);
    let generation = 0;
    let loading = false;

    // Keep helpers as expressions so bundle minification preserves their lexical scope.
    const updatePathState = () => {
        const hasNamespace = namespace.value !== '';
        namespace.disabled = !hasNamespace && (rootPath.checked || !canUseNamespaces || loading);
        rootPath.disabled = !canCreateRoot && !rootPath.checked;
        createButton.disabled = !canCreate ||
            (rootPath.checked && (!canCreateRoot || hasNamespace)) ||
            (hasNamespace && (!canUseNamespaces || namespace.selectedOptions[0].dataset.unavailable === 'true'));
    };

    const refreshNamespaces = () => {
        const current = ++generation;
        const selectedOrigin = origin.value;
        namespace.replaceChildren(defaultOption.cloneNode(true));
        loading = false;
        retry.hidden = true;
        updatePathState();
        if (!canUseNamespaces || !selectedOrigin) {
            if (canUseNamespaces) {
                status.textContent = resource('Namespaces:SelectDomainHelp');
            }
            return;
        }

        loading = true;
        status.textContent = resource('Namespaces:Loading');
        updatePathState();
        abp.ajax({
            url: createForm.dataset.namespaceListUrl,
            type: 'GET',
            data: { origin: selectedOrigin },
            contentType: 'application/x-www-form-urlencoded; charset=UTF-8'
        }).done(result => {
            if (current !== generation || selectedOrigin !== origin.value) {
                return;
            }
            result.items.forEach(item => {
                const option = document.createElement('option');
                option.value = item.id;
                option.textContent = `${item.name} (/${item.name}/{code})`;
                namespace.append(option);
            });
            loading = false;
            status.textContent = resource(result.items.length ? 'Namespaces:SelectHelp' : 'Namespaces:NoneForDomain');
            updatePathState();
        }).fail(() => {
            if (current !== generation || selectedOrigin !== origin.value) {
                return;
            }
            loading = false;
            status.textContent = resource('Namespaces:LoadFailed');
            retry.hidden = false;
            updatePathState();
        });
    };

    origin.addEventListener('change', refreshNamespaces);
    retry.addEventListener('click', refreshNamespaces);
    namespace.addEventListener('change', () => {
        if (namespace.value) {
            rootPath.checked = false;
        }
        updatePathState();
    });
    rootPath.addEventListener('change', () => {
        if (rootPath.checked) {
            namespace.value = '';
        }
        updatePathState();
    });
    updatePathState();
});
