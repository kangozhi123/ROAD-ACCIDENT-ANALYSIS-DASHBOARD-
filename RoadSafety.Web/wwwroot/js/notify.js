/* Brand-themed wrapper around SweetAlert2.
   Keeps colours and wording consistent so pages never call Swal directly. */
window.notify = (function () {
    var brand = {
        azure: '#2478E8',
        slate: '#7C8AA0',
        ink: '#17203A'
    };

    var base = {
        confirmButtonColor: brand.azure,
        cancelButtonColor: brand.slate,
        color: brand.ink,
        buttonsStyling: true
    };

    function merge(extra) {
        var out = {};
        Object.keys(base).forEach(function (k) { out[k] = base[k]; });
        Object.keys(extra).forEach(function (k) { out[k] = extra[k]; });
        return out;
    }

    return {
        /** Brief confirmation that slides in and leaves on its own. */
        toast: function (message, icon) {
            return Swal.fire(merge({
                toast: true,
                position: 'top-end',
                icon: icon || 'success',
                title: message,
                showConfirmButton: false,
                timer: 3200,
                timerProgressBar: true
            }));
        },

        /** Blocking question. Resolves true only when the user confirms. */
        confirm: function (options) {
            return Swal.fire(merge({
                icon: 'question',
                title: options.title,
                text: options.text,
                showCancelButton: true,
                confirmButtonText: options.confirmText || 'Continue',
                cancelButtonText: 'Cancel',
                reverseButtons: true
            })).then(function (result) { return result.isConfirmed === true; });
        },

        error: function (message) {
            return Swal.fire(merge({
                icon: 'error',
                title: 'Something went wrong',
                text: message
            }));
        }
    };
})();
