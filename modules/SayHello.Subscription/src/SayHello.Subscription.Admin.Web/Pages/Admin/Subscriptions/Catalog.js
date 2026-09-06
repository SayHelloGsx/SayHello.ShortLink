$(function () {
    'use strict';
    const s = window.subscriptionAdmin, l = s.l, root = $('#subscription-catalog');
    const area = root.data('area'), products = area === 'Products', plans = area === 'Plans';
    let skip = 0, editing = null, selected = [], features = [], definitionReady = false, listGeneration = 0, editorGeneration = 0;
    const results = $('#catalog-results'), editor = $('#catalog-editor');

    async function load(offset) {
        skip = offset;
        const generation = ++listGeneration, size = Number($('#page-size').val());
        const query = { filter: $('#filter').val(), sorting: $('#sorting').val(), skipCount: skip, maxResultCount: size };
        $('#catalog-filter-validation').empty();
        if ($('#catalog-product').val()) {
            const productId = $('#catalog-product').val().trim();
            if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(productId)) {
                $('#catalog-filter-validation').text(l('InvalidId')); return;
            }
            query.productId = productId;
        }
        if ($('#state').val() !== '') query.state = $('#state').val();
        const page = await s.request('List', query);
        if (generation !== listGeneration) return;
        if (!page.items.length && skip > 0) return load(Math.max(0, skip - size));
        results.empty();
        const table = $('<table class="table table-striped align-middle">'), body = $('<tbody>');
        table.append($('<thead>').append($('<tr>').append(
            ['Code', 'Name', 'Details', 'State', 'Actions'].map(key => $('<th>').text(l(key))))), body);
        page.items.forEach(item => {
            const actions = $('<td class="subscription-actions">');
            actions.append(s.button(l('Details'), () => showDetails(item)));
            if (root.data('update') && item.state !== 3) actions.append(s.button(l('Update'), () => edit(item.id)));
            if (root.data('publish') && item.state !== 3) {
                [1, 2, 3].filter(state => state !== item.state).forEach(state =>
                    actions.append(s.button(l('ActionState:' + state), async () => {
                        if (!await s.confirm(l('ConfirmState', l('ActionState:' + state), item.name))) return;
                        await s.request('State', { concurrencyStamp: item.concurrencyStamp, state }, 'POST', item.id);
                        abp.notify.success(l('Saved')); await load(skip);
                    })));
            }
            if (root.data('delete')) actions.append(s.button(l('Delete'), async () => {
                if (!await s.confirm(l('ConfirmDelete', item.name))) return;
                await s.request('Delete', { concurrencyStamp: item.concurrencyStamp }, 'POST', item.id);
                abp.notify.success(l('Deleted')); await load(skip);
            }, 'btn-outline-danger'));
            const detail = plans ? item.productName : products ? item.description : item.items.map(x => x.productName + ': ' + x.planName).join('; ');
            body.append($('<tr>').append($('<td>').text(item.code), $('<td>').text(item.name),
                $('<td class="subscription-wrap">').text(detail), $('<td>').text(l('State:' + item.state)), actions));
        });
        results.append($('<div class="table-responsive">').append(table));
        if (!page.items.length) results.append(s.text(l('NoResults')));
        s.pager($('#catalog-pager'), skip, size, page.totalCount, load);
    }

    async function showDetails(item) {
        const section = $('<section class="card p-3 mt-3">').append($('<h2>').text(item.name), $('<p>').text(item.description),
            $('<p>').text('ID: ' + item.id),
            $('<p>').text(l('DisplayOrder') + ': ' + item.displayOrder));
        if (plans) section.append($('<p>').text(l('ProductId') + ': ' + item.productId), s.entitlements(item.entitlements));
        if (!products && !plans) item.items.forEach(component => section.append(
            $('<h3 class="h5">').text(component.productName + ' / ' + component.planName), s.entitlements(component.entitlements)));
        section.append(s.button(l('Close'), () => section.remove()));
        results.find('section').remove(); results.append(section);
        if (products) {
            const definitions = await s.request('Definitions');
            const definition = definitions.items.find(x => x.code === item.code);
            if (definition) {
                const list = $('<ul>');
                definition.features.forEach(feature => list.append($('<li>').text(
                    feature.displayName + ' (' + feature.key + ') — ' + l(feature.type === 0 ? 'Boolean' : 'Finite') +
                    (feature.type === 1 ? '; ' + l('Maximum', feature.maximum || '9223372036854775807') : '') +
                    (feature.allowUnlimited ? '; ' + l('Unlimited') : '') +
                    (feature.description ? ': ' + feature.description : ''))));
                section.append($('<h3 class="h5">').text(l('Entitlements')), definition.features.length ? list : s.text(l('NoFeatures')));
            }
        }
    }

    function renderSelected() {
        const target = $('#selected-components').empty();
        selected.forEach(item => target.append($('<div class="my-1">').append(
            s.text(plans ? item.name : item.productName + ' / ' + item.name),
            (!plans || !editing) ? s.button(l('Remove'), () => {
                selected = selected.filter(x => x.id !== item.id);
                renderSelected();
                if (plans) { features = []; definitionReady = false; $('#entitlement-fields').empty(); $('#save-item').prop('disabled', true); }
            }).addClass('ms-2') : null)));
    }

    function renderFeatures(definition, existing) {
        features = definition.features;
        definitionReady = true;
        $('#save-item').prop('disabled', false);
        const container = $('#entitlement-fields').empty();
        if (!features.length) container.append(s.text(l('NoFeatures')));
        features.forEach((feature, index) => {
            const configured = (existing || []).find(x => x.featureKey === feature.key);
            const value = configured ? configured.value : null;
            const row = $('<div class="border rounded p-2 mb-2">').attr('data-feature', index);
            const id = 'feature-' + index;
            const configuredInput = $('<input type="checkbox" class="form-check-input feature-configured">')
                .attr('id', 'configured-' + index).prop('checked', !!configured);
            row.append($('<div class="form-check mb-2">').append(configuredInput,
                $('<label class="form-check-label">').attr('for', 'configured-' + index).text(l('Configured'))));
            row.append($('<label class="form-label">').attr('for', id).text(feature.displayName + ' (' + feature.key + ')'));
            if (feature.description) row.append($('<p class="small">').text(feature.description));
            if (feature.type === 0) {
                row.append($('<select class="form-select feature-boolean">').attr('id', id).append(
                    $('<option value="false">').text(l('Disabled')), $('<option value="true">').text(l('Enabled')))
                    .val(value && value.booleanValue ? 'true' : 'false'));
            } else {
                const mode = $('<select class="form-select feature-mode mb-2">').attr('id', id).append(
                    $('<option value="finite">').text(l('Finite')));
                if (feature.allowUnlimited) mode.append($('<option value="unlimited">').text(l('Unlimited')));
                const number = $('<input class="form-control feature-number" inputmode="numeric" pattern="[0-9]+" maxlength="19">')
                    .attr('aria-label', feature.displayName + ' ' + l('Finite')).val(value && value.numericValue != null ? value.numericValue : '0');
                mode.val(value && value.isUnlimited ? 'unlimited' : 'finite');
                function updateMode() {
                    const enabled = configuredInput.prop('checked');
                    mode.prop('disabled', !enabled);
                    number.prop('disabled', !enabled || mode.val() === 'unlimited').prop('required', enabled && mode.val() !== 'unlimited');
                }
                mode.on('change', updateMode); configuredInput.on('change', updateMode); updateMode();
                row.append(mode, number, $('<small>').text(l('Maximum', feature.maximum || '9223372036854775807')));
            }
            function updateBoolean() { row.find('.feature-boolean').prop('disabled', !configuredInput.prop('checked')); }
            configuredInput.on('change', updateBoolean); updateBoolean();
            container.append(row);
        });
    }

    async function edit(id) {
        const generation = ++editorGeneration;
        const item = id ? await s.request('Item', { id }) : null;
        if (generation !== editorGeneration) return;
        editing = item; selected = []; features = []; definitionReady = false;
        $('#save-item').prop('disabled', plans);
        $('#catalog-form')[0].reset(); $('#editor-validation').empty(); $('#entitlement-fields').empty();
        $('#editor-title').text(l(item ? 'Update' : 'Create'));
        $('#item-name').val(item ? item.name : '');
        $('#item-description').val(item ? item.description : '');
        $('#item-order').val(item ? item.displayOrder : 0);
        $('#item-code').val(item ? item.code : '').prop('disabled', !!item).prop('required', !products);
        $('#code-field').prop('hidden', products);
        $('#product-definition').prop('hidden', !products);
        $('#component-editor').prop('hidden', products);
        $('#entitlement-editor').prop('hidden', !plans);
        editor.prop('hidden', false);
        if (products) {
            const definitions = await s.request('Definitions');
            if (generation !== editorGeneration) return;
            const codes = $('#registered-code').empty().prop('disabled', !!item).prop('required', true);
            codes.append($('<option value="">').text(l('Select')));
            definitions.items.forEach(def => codes.append($('<option>').val(def.code).text(def.displayName + ' (' + def.code + ')')));
            codes.val(item ? item.code : '');
            if (!definitions.items.length) $('#editor-validation').text(l('NoRegisteredProducts'));
        } else if (plans) {
            $('#component-title').text(l('Product')); $('#component-help').text(l('PlanProductHelp'));
            if (item) {
                selected = [{ id: item.productId, name: item.productName }];
                $('#component-picker').empty();
                const definition = await s.request('Definition', { productId: item.productId });
                if (generation !== editorGeneration) return;
                renderFeatures(definition, item.entitlements);
            } else {
                s.picker($('#component-picker'), 'Options', async product => {
                    const current = ++editorGeneration;
                    selected = [product]; renderSelected(); features = []; definitionReady = false;
                    $('#save-item').prop('disabled', true); $('#entitlement-fields').empty();
                    const definition = await s.request('Definition', { productId: product.id });
                    if (current === editorGeneration) renderFeatures(definition, []);
                }, product => product.name + ' (' + product.code + ')');
            }
        } else {
            $('#component-title').text(l('Plans')); $('#component-help').text(l('BundleCompositionHelp'));
            selected = item ? item.items.map(x => ({ id: x.planId, productId: x.productId, productName: x.productName, name: x.planName })) : [];
            s.picker($('#component-picker'), 'Options', plan => {
                if (selected.some(x => x.productId === plan.productId)) {
                    $('#editor-validation').text(l('DuplicateProduct')); return;
                }
                selected.push(plan); $('#editor-validation').empty(); renderSelected();
            }, plan => plan.productName + ' / ' + plan.name + ' (' + l('State:' + plan.state) + ')');
        }
        renderSelected();
        $('#item-name').trigger('focus');
        editor[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function readEntitlements() {
        return features.flatMap((feature, index) => {
            const row = $('[data-feature="' + index + '"]');
            if (!row.find('.feature-configured').prop('checked')) return [];
            if (feature.type === 0) return { featureKey: feature.key, value: {
                type: 0, booleanValue: row.find('.feature-boolean').val() === 'true', numericValue: null, isUnlimited: false } };
            const unlimited = row.find('.feature-mode').val() === 'unlimited';
            const number = row.find('.feature-number').val();
            if (!unlimited && (!/^\d+$/.test(number) || BigInt(number) > BigInt(feature.maximum || '9223372036854775807')))
                throw new Error(l('InvalidNumeric', feature.displayName));
            return { featureKey: feature.key, value: {
                type: 1, booleanValue: null, numericValue: unlimited ? null : BigInt(number).toString(), isUnlimited: unlimited } };
        });
    }

    $('#catalog-form').on('submit', async function (event) {
        event.preventDefault();
        if (!this.reportValidity()) return;
        $('#editor-validation').empty();
        let input;
        try {
            input = { name: $('#item-name').val().trim(), description: $('#item-description').val(),
                displayOrder: Number($('#item-order').val()), code: products ? $('#registered-code').val() : $('#item-code').val(),
                concurrencyStamp: editing ? editing.concurrencyStamp : undefined };
            if (!input.name) throw new Error(l('NameRequired'));
            if (plans) {
                if (selected.length !== 1) throw new Error(l('SelectProduct'));
                if (!definitionReady) throw new Error(l('LoadingDefinition'));
                input.productId = selected[0].id;
                input.entitlements = readEntitlements();
            } else if (!products) {
                if (selected.length < 2) throw new Error(l('BundleCompositionHelp'));
                input.planIds = selected.map(x => x.id);
            }
        } catch (error) { $('#editor-validation').text(error.message); return; }
        $('#save-item').prop('disabled', true);
        try {
            await s.request(editing ? 'Update' : 'Create', input, 'POST', editing && editing.id);
            editor.prop('hidden', true); ++editorGeneration; abp.notify.success(l('Saved')); await load(skip);
        } finally { $('#save-item').prop('disabled', false); }
    });
    $('#catalog-filter').on('submit', event => { event.preventDefault(); load(0); });
    $('#create-item').on('click', () => edit(null));
    $('#cancel-edit').on('click', () => { ++editorGeneration; editor.prop('hidden', true); });
    load(0);
});
