const THEME_COOKIE = "bzs-theme";
const MEDIA_DARK = "(prefers-color-scheme: dark)";

function getCookie(name) {
    const match = document.cookie
        .split("; ")
        .find((row) => row.startsWith(name + "="));
    return match ? decodeURIComponent(match.split("=")[1]) : null;
}

/**
 * Read the bzs-theme cookie value.
 * Returns "light" | "dark" | "system" | null
 */
export function getThemeCookie() {
    return getCookie(THEME_COOKIE);
}

/**
 * Resolve and apply theme to <html data-theme="...">
 * "system" → checks prefers-color-scheme and maps to "light" or "dark"
 */
export function applyTheme(theme) {
    let resolved = theme;
    if (theme === "system") {
        resolved = window.matchMedia(MEDIA_DARK).matches ? "dark" : "light";
    }
    document.documentElement.setAttribute("data-theme", resolved);
}

/**
 * Initialize the widget: register click-outside listener.
 * @param {HTMLElement} widgetEl - the .pref-widget root element
 * @param {DotNetObjectReference} dotNetRef - Blazor component ref
 */
export function init(widgetEl, dotNetRef) {
    if (!widgetEl) return;

    const onClickOutside = (event) => {
        if (!widgetEl.contains(event.target)) {
            dotNetRef.invokeMethodAsync("ClosePanel");
        }
    };

    document.addEventListener("click", onClickOutside, { passive: true });

    // Store cleanup on element for disposal
    widgetEl._prefCleanup = () => {
        document.removeEventListener("click", onClickOutside);
    };
}

/**
 * Clean up event listeners.
 */
export function dispose(widgetEl) {
    if (widgetEl && typeof widgetEl._prefCleanup === "function") {
        widgetEl._prefCleanup();
        delete widgetEl._prefCleanup;
    }
}
