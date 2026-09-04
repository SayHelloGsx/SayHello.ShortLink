document.addEventListener('DOMContentLoaded', () => {
    const resource = abp.localization.getResource('ShortLink');

    document.querySelectorAll('.delete-short-link-form').forEach(form => {
        form.addEventListener('submit', event => {
            if (!window.confirm(resource('DeleteConfirmation'))) {
                event.preventDefault();
            }
        });
    });
});
