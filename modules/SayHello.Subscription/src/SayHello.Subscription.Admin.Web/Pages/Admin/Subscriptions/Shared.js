(function () {
    'use strict';
    const l = abp.localization.getResource('SubscriptionAdmin');
    const text = value => $('<span>').text(value == null ? '' : String(value));
    const button = (label, click, style) => $('<button type="button">')
        .addClass('btn btn-sm ' + (style || 'btn-outline-primary')).text(label).on('click', click);

    function request(handler, data, method, id) {
        const headers = {};
        headers[abp.security.antiForgery.tokenHeaderName] =
            $('input[name="__RequestVerificationToken"]').first().val() || abp.security.antiForgery.getToken();
        const url = location.pathname + '?handler=' + encodeURIComponent(handler) +
            (id ? '&id=' + encodeURIComponent(id) : '');
        return abp.ajax({
            url: url, type: method || 'GET', headers: headers,
            data: method === 'POST' ? JSON.stringify(data).replace(/"numericValue":"(\d+)"/g, '"numericValue":$1') : data,
            contentType: method === 'POST' ? 'application/json' : 'application/x-www-form-urlencoded; charset=UTF-8',
            // Preserve Int64 limits through the browser instead of rounding them to IEEE-754 doubles.
            dataFilter: raw => raw.replace(/"(numericValue|maximum)":\s*(\d+)/g, '"$1":"$2"')
        });
    }

    function pager(container, skip, size, total, load) {
        container.empty().append(
            button(l('Previous'), () => load(Math.max(0, skip - size))).prop('disabled', skip === 0),
            text(l('PageSummary', total === 0 ? 0 : skip + 1, Math.min(skip + size, total), total)).addClass('mx-3'),
            button(l('Next'), () => load(skip + size)).prop('disabled', skip + size >= total));
    }

    function picker(container, handler, select, label, extra) {
        container.empty();
        const searchId = 'picker-' + (++picker.sequence);
        const search = $('<input type="search" maxlength="256" class="form-control">').attr('id', searchId);
        const results = $('<div class="subscription-picker-results my-2">');
        const paging = $('<nav>').attr('aria-label', l('Pagination'));
        let skip = 0;
        let generation = 0;
        async function load(offset) {
            skip = offset;
            const current = ++generation;
            const page = await request(handler, Object.assign({ filter: search.val(), skipCount: skip, maxResultCount: 10 }, extra || {}));
            if (current !== generation) return;
            results.empty();
            if (!page.items.length) results.append(text(l('NoResults')));
            page.items.forEach(item => results.append(
                $('<div class="d-flex justify-content-between gap-2 py-1">').append(text(label(item)), button(l('Select'), () => select(item)))));
            pager(paging, skip, 10, page.totalCount, load);
        }
        const find = button(l('Search'), () => load(0));
        search.on('keydown', event => { if (event.key === 'Enter') { event.preventDefault(); load(0); } });
        container.append($('<label>').attr('for', searchId).text(l('Search')),
            $('<div class="input-group">').append(search, find), results, paging);
        load(0);
        return { reload: () => load(0) };
    }
    picker.sequence = 0;

    function entitlements(values) {
        const list = $('<ul class="mb-0">');
        (values || []).forEach(item => list.append($('<li>').text(item.displayName + ': ' +
            (item.value.type === 0 ? l(item.value.booleanValue ? 'Enabled' : 'Disabled') :
                item.value.isUnlimited ? l('Unlimited') : item.value.numericValue))));
        return list;
    }

    function date(value) { return value ? new Date(value).toLocaleString() : l('Never'); }
    function expiration(value) {
        if (!value) return null;
        const instant = new Date(value);
        if (Number.isNaN(instant.getTime()) || instant.getTime() <= Date.now()) throw new Error(l('FutureExpiration'));
        return instant.toISOString();
    }
    function confirm(message) { return abp.message.confirm(message); }
    window.subscriptionAdmin = { l, text, button, request, pager, picker, entitlements, date, expiration, confirm };
})();
