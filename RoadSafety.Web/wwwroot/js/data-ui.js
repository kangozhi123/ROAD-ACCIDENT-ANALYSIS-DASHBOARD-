/* Shared defaults for DataTables, Select2 and Chart.js.
   Configured once here so every table, dropdown and chart in the project
   looks the same without each page repeating the options. */
(function () {
    var brand = {
        indigo: '#3A3FDF',
        azure: '#2478E8',
        cyan: '#12C7EE',
        warning: '#E8A32A',
        danger: '#D64550',
        slate: '#7C8AA0',
        line: '#E2E9F2',
        ink: '#17203A'
    };

    window.brandPalette = brand;

    /* Chart.js ---------------------------------------------------------- */
    if (window.Chart) {
        Chart.defaults.font.family =
            '"Segoe UI Variable Display", "Segoe UI", system-ui, sans-serif';
        Chart.defaults.font.size = 12;
        Chart.defaults.color = brand.slate;
        Chart.defaults.plugins.legend.labels.usePointStyle = true;
        Chart.defaults.plugins.legend.labels.boxWidth = 8;
        Chart.defaults.plugins.tooltip.backgroundColor = brand.ink;
        Chart.defaults.plugins.tooltip.padding = 10;
        Chart.defaults.plugins.tooltip.cornerRadius = 8;
        Chart.defaults.maintainAspectRatio = false;

        /** Series colours, in the order charts should use them. */
        window.chartSeries = [brand.azure, brand.indigo, brand.cyan, brand.warning, brand.danger];
    }

    /* DataTables -------------------------------------------------------- */
    if (window.jQuery && jQuery.fn && jQuery.fn.dataTable) {
        jQuery.extend(true, jQuery.fn.dataTable.defaults, {
            pageLength: 25,
            lengthMenu: [10, 25, 50, 100],
            // Wording aimed at officers reading collision records, not at
            // "entries" in the abstract.
            language: {
                search: '',
                searchPlaceholder: 'Search records',
                lengthMenu: 'Show _MENU_ rows',
                info: 'Showing _START_ to _END_ of _TOTAL_ records',
                infoEmpty: 'No records to show',
                infoFiltered: '(filtered from _MAX_ records)',
                zeroRecords: 'No records match that search',
                emptyTable: 'No records yet',
                paginate: { first: 'First', last: 'Last', next: 'Next', previous: 'Previous' }
            }
        });
    }

    /* Select2 ----------------------------------------------------------- */
    if (window.jQuery && jQuery.fn && jQuery.fn.select2) {
        jQuery.fn.select2.defaults.set('theme', 'bootstrap-5');
        jQuery.fn.select2.defaults.set('width', '100%');

        /** Call on any <select> to upgrade it in place. */
        window.enhanceSelect = function (selector, options) {
            return jQuery(selector).select2(options || {});
        };
    }
})();
