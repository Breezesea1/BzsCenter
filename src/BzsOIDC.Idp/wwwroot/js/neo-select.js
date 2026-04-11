export function init(rootElement, triggerId, dotNetRef) {
    if (!rootElement) {
        return;
    }

    const onClickOutside = (event) => {
        if (!rootElement.contains(event.target)) {
            dotNetRef.invokeMethodAsync("ClosePanel");
        }
    };

    const onFocusIn = (event) => {
        if (!rootElement.contains(event.target)) {
            dotNetRef.invokeMethodAsync("ClosePanel");
        }
    };

    const onKeyDown = (event) => {
        if (event.key === "Escape") {
            dotNetRef.invokeMethodAsync("ClosePanel");
            if (triggerId) {
                document.getElementById(triggerId)?.focus();
            }
        }
    };

    document.addEventListener("click", onClickOutside, { passive: true });
    document.addEventListener("focusin", onFocusIn);
    document.addEventListener("keydown", onKeyDown);

    rootElement._neoSelectCleanup = () => {
        document.removeEventListener("click", onClickOutside);
        document.removeEventListener("focusin", onFocusIn);
        document.removeEventListener("keydown", onKeyDown);
    };
}

export function syncActiveOption(rootElement, activeOptionId) {
    if (!rootElement || !activeOptionId) {
        return;
    }

    const activeElement = rootElement.querySelector(`#${CSS.escape(activeOptionId)}`);
    if (activeElement instanceof HTMLElement) {
        activeElement.scrollIntoView({ block: "nearest" });
    }
}

export function dispose(rootElement) {
    if (rootElement && typeof rootElement._neoSelectCleanup === "function") {
        rootElement._neoSelectCleanup();
        delete rootElement._neoSelectCleanup;
    }
}
