// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
    function initializeAdminGridEditors() {
        document.querySelectorAll('[data-admin-editor-form]').forEach(function (form) {
            var card = form.closest('.admin-module-card');
            var scope = card ? card.parentElement : document;
            var grid = scope.querySelector('[data-admin-editor-grid]');
            var help = scope.querySelector('[data-admin-editor-help]');
            var submit = form.querySelector('[data-admin-editor-submit]');
            var fields = Array.prototype.slice.call(form.querySelectorAll('[data-admin-field]'));

            if (!grid || fields.length === 0) {
                return;
            }

            function setFormEnabled(isEnabled) {
                fields.forEach(function (field) {
                    var isLocked = field.dataset.locked === 'true';
                    field.disabled = !isEnabled;
                    field.readOnly = isEnabled && isLocked;
                    field.classList.toggle('admin-input-locked', isEnabled && isLocked);
                });

                if (submit) {
                    submit.disabled = !isEnabled;
                }
            }

            function clearForm() {
                fields.forEach(function (field) {
                    field.value = '';
                });
            }

            function selectRow(row) {
                grid.querySelectorAll('.admin-selectable-row.is-selected').forEach(function (selectedRow) {
                    selectedRow.classList.remove('is-selected');
                    selectedRow.removeAttribute('aria-selected');
                });

                row.classList.add('is-selected');
                row.setAttribute('aria-selected', 'true');

                fields.forEach(function (field) {
                    var value = row.dataset['field' + field.dataset.adminField.charAt(0).toUpperCase() + field.dataset.adminField.slice(1)];
                    field.value = value || '';
                });

                setFormEnabled(true);

                if (help) {
                    help.textContent = 'Registro seleccionado. Los campos bloqueados se muestran solo para referencia.';
                }
            }

            clearForm();
            setFormEnabled(false);

            grid.querySelectorAll('.admin-selectable-row').forEach(function (row) {
                row.addEventListener('click', function () {
                    selectRow(row);
                });

                row.addEventListener('keydown', function (event) {
                    if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault();
                        selectRow(row);
                    }
                });
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeAdminGridEditors);
    } else {
        initializeAdminGridEditors();
    }
})();
