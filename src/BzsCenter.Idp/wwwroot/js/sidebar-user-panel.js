export function init(rootElement, dotNetRef) {
    if (!rootElement) {
        return;
    }

    const isInsideRoot = (event) => {
        if (!event) {
            return false;
        }

        if (typeof event.composedPath === "function") {
            const path = event.composedPath();
            if (Array.isArray(path) && path.includes(rootElement)) {
                return true;
            }
        }

        return rootElement.contains(event.target);
    };

    const onClickOutside = (event) => {
        if (!isInsideRoot(event)) {
            dotNetRef.invokeMethodAsync("CloseUserPanel");
        }
    };

    const onFocusIn = (event) => {
        if (!isInsideRoot(event)) {
            dotNetRef.invokeMethodAsync("CloseUserPanel");
        }
    };

    const onKeyDown = (event) => {
        if (event.key === "Escape") {
            dotNetRef.invokeMethodAsync("CloseUserPanel");
        }
    };

    document.addEventListener("click", onClickOutside, { passive: true });
    document.addEventListener("focusin", onFocusIn);
    document.addEventListener("keydown", onKeyDown);

    rootElement._sidebarUserPanelCleanup = () => {
        document.removeEventListener("click", onClickOutside);
        document.removeEventListener("focusin", onFocusIn);
        document.removeEventListener("keydown", onKeyDown);
    };
}

export function dispose(rootElement) {
    if (rootElement && typeof rootElement._sidebarUserPanelCleanup === "function") {
        rootElement._sidebarUserPanelCleanup();
        delete rootElement._sidebarUserPanelCleanup;
    }
}
