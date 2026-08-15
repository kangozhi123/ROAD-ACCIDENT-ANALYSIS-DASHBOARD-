/* Brand-themed wrapper around SweetAlert2.
   Keeps colours and wording consistent so pages never call Swal directly.

   Which one to reach for:
     success / failure — the outcome of an action the user just took
     alert             — a failure they must acknowledge before continuing
     confirm           — a question that must be answered before proceeding */
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

    /** Brief message that slides in and leaves on its own. */
    function toast(message, icon, holdMs) {
        return Swal.fire(merge({
            toast: true,
            position: 'top-end',
            icon: icon || 'success',
            title: message,
            showConfirmButton: false,
            timer: holdMs || 3200,
            timerProgressBar: true
        }));
    }

    return {
        toast: toast,

        success: function (message) {
            return toast(message, 'success');
        },

        /** A failed action. Held longer, because it asks the user to do something. */
        failure: function (message) {
            return toast(message, 'error', 5000);
        },

        warn: function (message) {
            return toast(message, 'warning', 4200);
        },

        /** Blocking. Use when the user must acknowledge before carrying on. */
        alert: function (message) {
            return Swal.fire(merge({
                icon: 'error',
                title: 'Something went wrong',
                text: message
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
        }
    };
})();
