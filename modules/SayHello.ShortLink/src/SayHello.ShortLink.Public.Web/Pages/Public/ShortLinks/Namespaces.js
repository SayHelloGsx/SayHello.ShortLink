document.addEventListener('DOMContentLoaded', () => {
    const resource = abp.localization.getResource('ShortLink');
    document.querySelectorAll('.delete-namespace-form').forEach(form => {
        form.addEventListener('submit', event => {
            event.preventDefault();
            abp.message.confirm(resource('Namespaces:DeleteConfirmation')).then(confirmed => {
                if (!confirmed) {
                    return;
                }
                const button = form.querySelector('button[type="submit"]');
                button.disabled = true;
                abp.ajax({
                    url: form.action,
                    type: 'POST',
                    data: $(form).serialize(),
                    contentType: 'application/x-www-form-urlencoded; charset=UTF-8'
                }).done(() => {
                    window.location.reload();
                }).always(() => {
                    button.disabled = false;
                });
            });
        });
    });
});
