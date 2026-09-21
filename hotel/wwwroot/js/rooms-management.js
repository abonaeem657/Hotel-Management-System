(() => {
    const container = document.getElementById('room-management-modals');
    if (!container) return;

    container.querySelectorAll('.modal').forEach(modal => {
        const forms = modal.querySelectorAll('.room-management-form');
        const errors = modal.querySelector('.room-management-errors');
        let saving = false;

        modal.addEventListener('hide.bs.modal', event => {
            if (saving) event.preventDefault();
        });

        modal.addEventListener('hidden.bs.modal', () => {
            forms.forEach(form => form.reset());
            if (errors) {
                errors.replaceChildren();
                errors.classList.add('d-none');
            }
            if (window.location.hash === '#' + modal.id) {
                history.replaceState(null, '', window.location.pathname + window.location.search);
            }
        });

        forms.forEach(form => {
            form.addEventListener('submit', async event => {
                event.preventDefault();
                if (saving || (form.dataset.confirm && !window.confirm(form.dataset.confirm))) return;

                const data = new FormData(form);
                const buttons = modal.querySelectorAll('button[type="submit"]');
                saving = true;
                buttons.forEach(button => button.disabled = true);
                errors.replaceChildren();
                errors.classList.add('d-none');

                try {
                    const response = await fetch(form.action, { method: 'POST', body: data });
                    if (response.redirected && new URL(response.url).pathname.includes('/Account/')) {
                        window.location.assign(response.url);
                        return;
                    }
                    if (response.ok && response.redirected) {
                        const target = container.dataset.indexUrl +
                            (form.dataset.reopenModal ? '#' + form.dataset.reopenModal : '');
                        // Assigning the current URL with only a fragment change would not reload the images.
                        window.history.replaceState(null, '', target);
                        window.location.reload();
                        return;
                    }

                    let messages = [response.status === 404
                        ? 'This room or image no longer exists. Please refresh the page.'
                        : 'Unable to complete this action. Please try again.'];
                    if (response.status === 400 &&
                        (response.headers.get('content-type') || '').includes('application/json')) {
                        const result = await response.json();
                        if (Array.isArray(result.errors) && result.errors.length) messages = result.errors;
                    }
                    messages.forEach(message => {
                        const item = document.createElement('div');
                        item.textContent = message;
                        errors.appendChild(item);
                    });
                    errors.classList.remove('d-none');
                } catch {
                    errors.textContent = 'Unable to complete this action. Check your connection and try again.';
                    errors.classList.remove('d-none');
                } finally {
                    saving = false;
                    buttons.forEach(button => button.disabled = false);
                }
            });
        });
    });

    const reopenModal = document.getElementById(window.location.hash.substring(1));
    if (reopenModal && container.contains(reopenModal) && reopenModal.classList.contains('modal')) {
        bootstrap.Modal.getOrCreateInstance(reopenModal).show();
    }
})();
