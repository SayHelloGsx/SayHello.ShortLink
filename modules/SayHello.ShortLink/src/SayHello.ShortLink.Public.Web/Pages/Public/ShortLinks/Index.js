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
});
