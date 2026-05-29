// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
    function getDataFieldName(fieldName) {
        return 'field' + fieldName.charAt(0).toUpperCase() + fieldName.slice(1);
    }

    function initializeAdminGridEditors() {
        document.querySelectorAll('[data-admin-editor-form]').forEach(function (form) {
            var scope = form.closest('[data-admin-editor-scope]') || document;
            var grid = scope.querySelector('[data-admin-editor-grid]');
            var search = scope.querySelector('[data-admin-grid-search]');
            var count = scope.querySelector('[data-admin-grid-count]');
            var help = scope.querySelector('[data-admin-editor-help]');
            var status = scope.querySelector('[data-admin-editor-status]');
            var summary = scope.querySelector('[data-admin-selected-summary]');
            var submit = form.querySelector('[data-admin-editor-submit]');
            var clear = form.querySelector('[data-admin-editor-clear]');
            var create = form.querySelector('[data-admin-editor-new]');
            var deleteForm = scope.querySelector('[data-admin-delete-form]');
            var deleteKey = deleteForm ? deleteForm.querySelector('[data-admin-delete-key]') : null;
            var deleteSubmit = deleteForm ? deleteForm.querySelector('[data-admin-delete-submit]') : null;
            var fields = Array.prototype.slice.call(form.querySelectorAll('[data-admin-field]'));

            if (!grid || fields.length === 0) {
                return;
            }

            var rows = Array.prototype.slice.call(grid.querySelectorAll('.admin-selectable-row'));

            function setFormEnabled(isEnabled, isCreate) {
                fields.forEach(function (field) {
                    var isLocked = field.dataset.locked === 'true';
                    var isGeneratedOnCreate = field.dataset.generatedOnCreate === 'true';
                    field.disabled = !isEnabled || (isCreate && isGeneratedOnCreate);
                    field.readOnly = isEnabled && !isCreate && isLocked;
                    field.classList.toggle('admin-input-locked', isEnabled && !isCreate && isLocked);
                });

                if (submit) {
                    submit.disabled = !isEnabled;
                }

                if (clear) {
                    clear.disabled = !isEnabled;
                }
            }

            function setDeleteEnabled(isEnabled, key) {
                if (deleteKey) {
                    deleteKey.value = key || '';
                }

                if (deleteSubmit) {
                    deleteSubmit.disabled = !isEnabled;
                }
            }

            function clearForm() {
                fields.forEach(function (field) {
                    field.value = '';
                });
            }

            function updateSummary(row, isCreate) {
                if (!summary) {
                    return;
                }

                summary.replaceChildren();

                var label = document.createElement('span');
                var titleElement = document.createElement('strong');
                summary.appendChild(label);
                summary.appendChild(titleElement);

                if (isCreate) {
                    label.textContent = 'Modo';
                    titleElement.textContent = 'Nuevo registro';
                    return;
                }

                if (!row) {
                    label.textContent = 'Registro';
                    titleElement.textContent = 'Ningún registro seleccionado';
                    return;
                }

                var title = row.dataset.summaryTitle || row.dataset.fieldName || row.dataset.fieldCode || 'Registro seleccionado';
                var detail = row.dataset.summaryDetail || row.dataset.fieldCode || '';
                label.textContent = 'Seleccionado';
                titleElement.textContent = title;

                if (detail) {
                    var detailElement = document.createElement('small');
                    detailElement.textContent = detail;
                    summary.appendChild(detailElement);
                }
            }

            function resetSelection() {
                grid.querySelectorAll('.admin-selectable-row.is-selected').forEach(function (selectedRow) {
                    selectedRow.classList.remove('is-selected');
                    selectedRow.removeAttribute('aria-selected');
                });

                clearForm();
                setFormEnabled(false, false);
                setDeleteEnabled(false, '');
                updateSummary(null, false);

                if (help) {
                    help.textContent = 'Selecciona una fila o crea un registro nuevo.';
                }

                if (status) {
                    status.textContent = 'Sin selección';
                    status.classList.remove('is-ready');
                }
            }

            function startCreate() {
                grid.querySelectorAll('.admin-selectable-row.is-selected').forEach(function (selectedRow) {
                    selectedRow.classList.remove('is-selected');
                    selectedRow.removeAttribute('aria-selected');
                });

                clearForm();
                setFormEnabled(true, true);
                setDeleteEnabled(false, '');
                updateSummary(null, true);

                if (help) {
                    help.textContent = 'Completa los datos del nuevo registro y presiona Guardar.';
                }

                if (status) {
                    status.textContent = 'Nuevo registro';
                    status.classList.add('is-ready');
                }
            }

            function selectRow(row) {
                grid.querySelectorAll('.admin-selectable-row.is-selected').forEach(function (selectedRow) {
                    selectedRow.classList.remove('is-selected');
                    selectedRow.removeAttribute('aria-selected');
                });

                row.classList.add('is-selected');
                row.setAttribute('aria-selected', 'true');

                fields.forEach(function (field) {
                    var value = row.dataset[getDataFieldName(field.dataset.adminField)];
                    field.value = value || '';
                });

                setFormEnabled(true, false);
                setDeleteEnabled(true, row.dataset.fieldCode || '');
                updateSummary(row, false);

                if (help) {
                    help.textContent = 'Registro cargado. Puedes modificarlo o eliminarlo.';
                }

                if (status) {
                    status.textContent = 'Listo para editar';
                    status.classList.add('is-ready');
                }
            }

            function updateVisibleCount(visibleRows) {
                if (count) {
                    count.textContent = visibleRows + ' de ' + rows.length + ' registros';
                }
            }

            function filterRows() {
                var query = search ? search.value.trim().toLowerCase() : '';
                var visibleRows = 0;

                rows.forEach(function (row) {
                    var matches = !query || row.textContent.toLowerCase().indexOf(query) !== -1;
                    row.hidden = !matches;
                    if (matches) {
                        visibleRows += 1;
                    }
                });

                updateVisibleCount(visibleRows);
            }

            resetSelection();
            updateVisibleCount(rows.length);

            rows.forEach(function (row) {
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

            if (search) {
                search.addEventListener('input', filterRows);
            }

            if (clear) {
                clear.addEventListener('click', resetSelection);
            }

            if (create) {
                create.addEventListener('click', startCreate);
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeAdminGridEditors);
    } else {
        initializeAdminGridEditors();
    }
})();
