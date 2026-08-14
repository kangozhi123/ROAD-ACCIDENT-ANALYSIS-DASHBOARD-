(function () {
    // Make sure lucide icons are created only if the library loaded
    if (window.lucide && typeof lucide.createIcons === 'function') {
        try {
            lucide.createIcons();
        } catch (e) {
            // silently ignore icon init errors
            console.warn('lucide.createIcons() failed', e);
        }
    }

    function handleLogin(event) {
        event.preventDefault();

        const forceNumberEl = document.getElementById('forceNumber');
        const passwordEl = document.getElementById('password');
        if (!forceNumberEl || !passwordEl) return;

        const forceNumber = forceNumberEl.value.trim();
        const password = passwordEl.value;

        if (!forceNumber || !password) {
            alert('Please enter your force number and password.');
            return;
        }

        fetch('login.php', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ forceNumber, password })
        })
        .then(response => response.json().then(data => ({ ok: response.ok, data })))
        .then(({ ok, data }) => {
            if (ok && data.success) {
                window.location.href = 'dashboard.php';
            } else {
                alert(data.error || 'Login failed. Please check your credentials.');
            }
        })
        .catch(error => {
            alert('Login error: ' + (error && error.message ? error.message : error));
        });
    }

    function handleCreateAccount(event) {
        if (event && event.preventDefault) event.preventDefault();

        const fullNameEl = document.getElementById('fullName');
        const newForceNumberEl = document.getElementById('newForceNumber');
        const newEmailEl = document.getElementById('newEmail');
        const newPasswordEl = document.getElementById('newPassword');
        const confirmPasswordEl = document.getElementById('confirmPassword');
        const newStationEl = document.getElementById('newStation');

        if (!fullNameEl || !newForceNumberEl || !newEmailEl || !newPasswordEl || !confirmPasswordEl || !newStationEl) return;

        const fullName = fullNameEl.value.trim();
        const newForceNumber = newForceNumberEl.value.trim();
        const newEmail = newEmailEl.value.trim();
        const newPassword = newPasswordEl.value;
        const confirmPassword = confirmPasswordEl.value;
        const newStation = newStationEl.value;

        if (!fullName || !newForceNumber || !newEmail || !newPassword || !confirmPassword || !newStation) {
            alert('Please complete all registration fields.');
            return;
        }

        if (newPassword !== confirmPassword) {
            alert('Passwords do not match. Please re-enter your password.');
            return;
        }

        fetch('register.php', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ fullName, newForceNumber, newEmail, newPassword, confirmPassword, newStation })
        })
        .then(response => response.json().then(data => ({ ok: response.ok, data })))
        .then(({ ok, data }) => {
            if (ok && data.success) {
                alert(data.message || 'Account created successfully.');
                window.location.href = 'index.php';
            } else {
                alert(data.error || 'Registration failed.');
            }
        })
        .catch(error => {
            alert('Registration error: ' + (error && error.message ? error.message : error));
        });
    }

    // Attach handlers after DOM is ready
    document.addEventListener('DOMContentLoaded', function () {
        // Attach to login form: prefer explicit onsubmit attribute, fallback to form containing #forceNumber
        let loginForm = document.querySelector('form[onsubmit="handleLogin(event)"]');
        if (!loginForm) {
            const forceNumberEl = document.getElementById('forceNumber');
            if (forceNumberEl) loginForm = forceNumberEl.closest('form');
        }
        if (loginForm && !loginForm.__loginHandlerAttached) {
            loginForm.addEventListener('submit', handleLogin);
            loginForm.__loginHandlerAttached = true;
        }

        // Attach to registration form on create-account.php if present
        let registerForm = document.querySelector('form[onsubmit="handleCreateAccount(event)"]');
        if (!registerForm) {
            const fullNameEl = document.getElementById('fullName');
            if (fullNameEl) registerForm = fullNameEl.closest('form');
        }
        if (registerForm && !registerForm.__registerHandlerAttached) {
            registerForm.addEventListener('submit', handleCreateAccount);
            registerForm.__registerHandlerAttached = true;
        }

        // If a dedicated create account button exists (index.html panel), wire it up
        const createAccountBtn = document.getElementById('createAccountBtn');
        if (createAccountBtn && !createAccountBtn.__handlerAttached) {
            createAccountBtn.addEventListener('click', handleCreateAccount);
            createAccountBtn.__handlerAttached = true;
        }

        // If there's a link to create-account.php on the page and an in-page panel exists, toggle it instead of navigating
        const createLink = document.querySelector('a[href="create-account.php"]');
        const createPanel = document.getElementById('createAccountPanel');
        if (createLink && createPanel) {
            createLink.addEventListener('click', function (ev) {
                ev.preventDefault();
                // toggle visibility
                if (createPanel.classList.contains('hidden')) {
                    createPanel.classList.remove('hidden');
                    createPanel.scrollIntoView({ behavior: 'smooth' });
                } else {
                    createPanel.classList.add('hidden');
                }
            });
        }
    });

    // expose functions for inline onsubmit usage on static pages
    window.handleLogin = handleLogin;
    window.handleCreateAccount = handleCreateAccount;
})();
