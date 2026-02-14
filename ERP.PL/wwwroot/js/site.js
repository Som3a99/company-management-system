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