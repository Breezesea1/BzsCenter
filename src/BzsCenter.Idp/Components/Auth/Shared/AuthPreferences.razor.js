const THEME_COOKIE = "bzs-theme";
const MEDIA_DARK = "(prefers-color-scheme: dark)";

function getCookie(name) {
    const match = document.cookie
        .split("; ")
        .find((row) => row.startsWith(name + "="));
    return match ? decodeURIComponent(match.split("=")[1]) : null;
}

export function getThemeCookie() {
    return getCookie(THEME_COOKIE);
}

export function applyTheme(theme) {
    let resolved = theme;
    if (theme === "system") {
        resolved = window.matchMedia(MEDIA_DARK).matches ? "dark" : "light";
    }

    document.documentElement.setAttribute("data-theme", resolved);
}

export function init(widgetEl, dotNetRef) {
    if (!widgetEl) {
        return;
    }

    const onClickOutside = (event) => {
        if (!widgetEl.contains(event.target)) {
            dotNetRef.invokeMethodAsync("ClosePanel");
        }
    };

    document.addEventListener("click", onClickOutside, { passive: true });

    widgetEl._prefCleanup = () => {
        document.removeEventListener("click", onClickOutside);
    };
}

export function dispose(widgetEl) {
    if (widgetEl && typeof widgetEl._prefCleanup === "function") {
        widgetEl._prefCleanup();
        delete widgetEl._prefCleanup;
    }
}
