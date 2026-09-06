$(function () {
    'use strict';
    const s = window.subscriptionAdmin, l = s.l, root = $('#subscription-users');
    let skip = 0, selected = null, preview = null, mutation = null, generation = 0, previewGeneration = 0;
    const guid = value => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) &&
        value !== '00000000-0000-0000-0000-000000000000';

    async function load(offset) {
        const query = { filter: $('#subscription-search').val(), status: $('#subscription-status').val(),
            currentOnly: $('#current-only').val(), sorting: $('#subscription-sort').val(),
            skipCount: offset, maxResultCount: Number($('#page-size').val()) };
        $('#filter-validation').empty();
        for (const [field, selector] of [['userId', '#user-id'], ['productId', '#product-id']]) {
            const value = $(selector).val().trim();
            if (value && !guid(value)) { $('#filter-validation').text(l('InvalidId')); return; }
            if (value) query[field] = value;
        }
        if (query.status === '') delete query.status;
        skip = offset;
        const current = ++generation, page = await s.request('List', query);
        if (generation !== current) return;
        if (!page.items.length && skip > 0) return load(Math.max(0, skip - query.maxResultCount));
        const results = $('#subscription-results').empty(), body = $('<tbody>');
        const table = $('<table class="table table-striped align-middle">').append(
            $('<thead>').append($('<tr>').append(['UserId', 'Product', 'Plan', 'Status', 'StartsAt', 'ExpiresAt', 'Actions'].map(x => $('<th>').text(l(x))))), body);
        page.items.forEach(item => {
            const actions = $('<td class="subscription-actions">').append(s.button(l('Details'), () => details(item)));
            if (item.status === 0 && root.data('revoke')) actions.append(s.button(l('Revoke'), () => editMutation(item, 'Revoke'), 'btn-outline-danger'));
            if (item.status === 0 && root.data('adjust')) actions.append(s.button(l('AdjustExpiration'), () => editMutation(item, 'Expiration')));
            const productCell = $('<td>').append(s.text(item.productName), s.button(l('FilterProduct'), () => {
                $('#product-id').val(item.productId); load(0);
            }).addClass('d-block mt-1'));
            body.append($('<tr>').append($('<td class="subscription-wrap">').text(item.userId), productCell,
                $('<td>').text(item.planName), $('<td>').text(l('Status:' + item.status)), $('<td>').text(s.date(item.startsAt)),
                $('<td>').text(s.date(item.expiresAt)), actions));
        });
        results.append($('<div class="table-responsive">').append(table));
        if (!page.items.length) results.append(s.text(l('NoResults')));
        s.pager($('#subscription-pager'), skip, query.maxResultCount, page.totalCount, load);
    }

    function details(item) {
        const container = $('#subscription-results'); container.find('section').remove();
        const detail = $('<section class="card p-3">').append($('<h2>').text(item.productName + ' / ' + item.planName),
            $('<p>').text(l('SubscriptionId') + ': ' + item.id),
            $('<p>').text(l('ProductId') + ': ' + item.productId),
            $('<p>').text(l('SourceBundle') + ': ' + (item.bundleName || l('None'))),
            $('<p>').text(l('AssignmentId') + ': ' + item.assignmentId),
            $('<p>').text(l('EndedAt') + ': ' + (item.endedAt ? s.date(item.endedAt) : l('None'))),
            $('<p>').text(l('Reason') + ': ' + (item.endReason == null ? l('None') : l('EndReason:' + item.endReason)) +
                (item.endReasonDetail ? ' — ' + item.endReasonDetail : '')),
            $('<p>').text(l('SnapshotHelp')), s.entitlements(item.entitlements),
            s.button(l('Close'), () => detail.remove()));
        container.append(detail);
    }

    function invalidatePreview() {
        preview = null; ++previewGeneration; $('#assignment-form').prop('hidden', true);
        $('#assignment-validation').empty();
    }
    $('#user-id').on('input', () => { $('#selected-user').empty(); invalidatePreview(); });
    if ($('#user-picker').length) s.picker($('#user-picker'), 'Lookup', user => {
        $('#user-id').val(user.id); $('#selected-user').text(user.userName + ' — ' + (user.email || ''));
        invalidatePreview(); load(0);
    }, user => user.userName + (user.displayName ? ' / ' + user.displayName : '') +
        (user.email ? ' / ' + user.email : '') + (user.isActive ? '' : ' (' + l('Inactive') + ')'));

    function assignmentPicker() {
        selected = null; invalidatePreview(); $('#selected-assignment').empty();
        const bundle = $('#assignment-kind').val() === 'bundle';
        s.picker($('#assignment-picker'), bundle ? 'Bundles' : 'Plans', item => {
            selected = item; invalidatePreview();
            $('#selected-assignment').text(bundle ? item.name : item.productName + ' / ' + item.name);
        }, item => bundle ? item.name + ' (' + item.code + ')' : item.productName + ' / ' + item.name + ' (' + item.code + ')');
    }
    $('#assignment-kind').on('change', assignmentPicker);
    if ($('#assignment-picker').length) assignmentPicker();

    $('#preview-assignment').on('click', async () => {
        invalidatePreview();
        const userId = $('#user-id').val().trim(), bundle = $('#assignment-kind').val() === 'bundle';
        if (!guid(userId) || !selected) { $('#assignment-validation').text(l('SelectUserAndPlan')); return; }
        const current = previewGeneration;
        const result = await s.request('Preview', { userId, id: selected.id, bundle });
        if (current !== previewGeneration) return;
        preview = result;
        $('#preview-user').text(l('UserId') + ': ' + preview.userId);
        const items = $('#preview-items').empty();
        preview.items.forEach((item, index) => {
            const id = 'expires-' + index;
            const section = $('<section class="border p-3 mb-3">').append(
                $('<h4>').text(item.productName + ' / ' + item.planName),
                $('<p>').text(item.expectedCurrent ? l('WillReplace', item.expectedCurrent.subscriptionId, s.date(item.currentExpiresAt)) : l('WillCreate')),
                s.entitlements(item.entitlements), $('<label class="mt-2">').attr('for', id).text(l('ExpiresAt')),
                $('<input type="datetime-local" class="form-control product-expiration">').attr('id', id));
            items.append(section);
        });
        $('#assignment-form').prop('hidden', false);
    });
    $('#fill-all').on('click', () => $('.product-expiration').val($('#fill-expiration').val()));
    $('#clear-all').on('click', () => $('.product-expiration').val(''));
    $('#assignment-form').on('submit', async function (event) {
        event.preventDefault();
        if (!preview || !this.reportValidity()) return;
        const captured = preview, targets = [];
        $('#assignment-validation').empty();
        try {
            captured.items.forEach((item, index) => targets.push({
                productId: item.productId, planId: item.planId, productConcurrencyStamp: item.productConcurrencyStamp,
                planConcurrencyStamp: item.planConcurrencyStamp, expectedCurrent: item.expectedCurrent,
                expiresAt: s.expiration($('#expires-' + index).val())
            }));
        } catch (error) { $('#assignment-validation').text(error.message); return; }
        const summary = l('ConfirmAssignmentFor', captured.userId) + '\n' + captured.items.map((item, index) =>
            item.productName + ' / ' + item.planName + ' — ' + s.date(targets[index].expiresAt) +
            ' (' + l(item.expectedCurrent ? 'Replace' : 'Create') + ')').join('\n') + '\n' + l('ReplacementWarning');
        if (!await s.confirm(summary) || captured !== preview) return;
        $('#confirm-assignment').prop('disabled', true);
        try {
            await s.request(captured.bundleId ? 'AssignBundle' : 'AssignPlan', captured.bundleId ? {
                userId: captured.userId, bundleId: captured.bundleId, bundleConcurrencyStamp: captured.bundleConcurrencyStamp, targets
            } : { userId: captured.userId, target: targets[0] }, 'POST');
            invalidatePreview(); abp.notify.success(l('Assigned')); await load(0);
        } finally { $('#confirm-assignment').prop('disabled', false); }
    });

    function editMutation(item, operation) {
        mutation = { item, operation };
        $('#mutation-title').text(l(operation === 'Revoke' ? 'Revoke' : 'AdjustExpiration'));
        $('#mutation-target').text(item.userId + ' / ' + item.productName + ' / ' + item.planName);
        $('#reason-field').prop('hidden', operation !== 'Revoke');
        $('#expiration-field').prop('hidden', operation === 'Revoke');
        $('#revoke-reason').val(''); $('#mutation-validation').empty();
        if (item.expiresAt) {
            const date = new Date(item.expiresAt);
            $('#mutation-expiration').val(new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16));
        } else $('#mutation-expiration').val('');
        $('#mutation-editor').prop('hidden', false)[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    $('#mutation-form').on('submit', async function (event) {
        event.preventDefault();
        if (!mutation || !this.reportValidity()) return;
        const captured = mutation, input = { concurrencyStamp: captured.item.concurrencyStamp };
        try {
            if (captured.operation === 'Revoke') input.reason = $('#revoke-reason').val();
            else input.expiresAt = s.expiration($('#mutation-expiration').val());
        } catch (error) { $('#mutation-validation').text(error.message); return; }
        const summary = captured.operation === 'Revoke' ? l('ConfirmRevoke', captured.item.productName, captured.item.userId) :
            l('ConfirmExpiration', captured.item.productName, s.date(input.expiresAt));
        if (!await s.confirm(summary) || captured !== mutation) return;
        $('#save-mutation').prop('disabled', true);
        try {
            await s.request(captured.operation, input, 'POST', captured.item.id);
            $('#mutation-editor').prop('hidden', true); mutation = null; invalidatePreview();
            abp.notify.success(l('Saved')); await load(skip);
        } finally { $('#save-mutation').prop('disabled', false); }
    });
    $('#cancel-mutation').on('click', () => { mutation = null; $('#mutation-editor').prop('hidden', true); });
    $('#subscription-filter').on('submit', event => { event.preventDefault(); load(0); });
    load(0);
});
