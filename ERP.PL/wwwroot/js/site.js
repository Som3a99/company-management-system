(function () {
    const storageKey = 'erp-theme';
    const root = document.documentElement;
    const toggleButtons = document.querySelectorAll('[data-theme-toggle]');

    if (!root || toggleButtons.length === 0) {
        return;
    }

    const updateToggleUi = (theme) => {
        const isDark = theme === 'dark';

        toggleButtons.forEach((button) => {
            const icon = button.querySelector('[data-theme-icon]');
            if (icon) {
                icon.className = isDark ? 'fas fa-sun' : 'fas fa-moon';
            }

            button.setAttribute('aria-label', isDark ? 'Switch to light mode' : 'Switch to dark mode');
            button.setAttribute('title', isDark ? 'Switch to light mode' : 'Switch to dark mode');
        });
    };

    const setTheme = (theme) => {
        root.setAttribute('data-theme', theme);
        localStorage.setItem(storageKey, theme);
        updateToggleUi(theme);
    };

    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const savedTheme = localStorage.getItem(storageKey);
    const initialTheme = savedTheme || (prefersDark ? 'dark' : 'light');

    setTheme(initialTheme);

    toggleButtons.forEach((button) => {
        button.addEventListener('click', () => {
            const currentTheme = root.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
            setTheme(currentTheme === 'dark' ? 'light' : 'dark');
        });
    });
})();

/* ── Navbar: Scroll Shadow ── */
(function () {
    const navbar = document.getElementById('mainNavbar');
    if (!navbar) return;

    let ticking = false;
    const onScroll = () => {
        if (!ticking) {
            requestAnimationFrame(() => {
                navbar.classList.toggle('scrolled', window.scrollY > 8);
                ticking = false;
            });
            ticking = true;
        }
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
})();

/* ── Navbar: Active Link Detection ── */
(function () {
    const currentPath = window.location.pathname.toLowerCase().replace(/\/+$/, '') || '/';
    const navLinks = document.querySelectorAll('.navbar-center .nav-link');

    navLinks.forEach((link) => {
        const href = (link.getAttribute('href') || '').toLowerCase().replace(/\/+$/, '') || '/';

        // Exact match or starts-with for sub-pages (but not just "/")
        const isActive = currentPath === href ||
            (href !== '/' && currentPath.startsWith(href + '/')) ||
            (href !== '/' && currentPath.startsWith(href));

        if (isActive) {
            link.classList.add('active');
            link.setAttribute('aria-current', 'page');
        }
    });
})();

/* ── Navbar: Close mobile menu on link click ── */
(function () {
    const navCollapse = document.getElementById('navbarNav');
    if (!navCollapse) return;

    navCollapse.addEventListener('click', (e) => {
        const link = e.target.closest('a.nav-link');
        if (link && window.innerWidth < 992) {
            const bsCollapse = bootstrap.Collapse.getInstance(navCollapse);
            if (bsCollapse) bsCollapse.hide();
        }
    });
})();