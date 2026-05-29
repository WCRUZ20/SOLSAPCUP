// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
(function () {
    const form = document.querySelector('[data-grid-edit-form]');
    const table = document.querySelector('[data-grid-edit-table]');

    if (!form || !table) {
        return;
    }

    const fields = Array.from(form.querySelectorAll('[data-field]'));
    const submitButton = form.querySelector('button[type="submit"]');
    let selectedRow = null;

    const setFormState = (row) => {
        fields.forEach((field) => {
            const fieldName = field.dataset.field;
            const value = row?.dataset[fieldName] ?? '';
            const isEditable = field.dataset.editable === 'true';

            field.value = value;
            field.disabled = !row;
            field.readOnly = Boolean(row) && !isEditable;
            field.classList.toggle('is-readonly', Boolean(row) && !isEditable);
        });

        if (submitButton) {
            submitButton.disabled = !row;
        }
    };

    const selectRow = (row) => {
        if (selectedRow) {
            selectedRow.classList.remove('is-selected');
        }

        selectedRow = row;
        selectedRow.classList.add('is-selected');
        setFormState(selectedRow);
    };

    table.querySelectorAll('tbody tr').forEach((row) => {
        row.addEventListener('click', () => selectRow(row));
        row.addEventListener('keydown', (event) => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                selectRow(row);
            }
        });
    });

    setFormState(null);
})();
