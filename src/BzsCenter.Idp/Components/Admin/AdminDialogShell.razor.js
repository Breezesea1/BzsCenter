function focusInitialField(dialogElement, selector) {
    if (!dialogElement) {
        return;
    }

    const target = selector ? dialogElement.querySelector(selector) : null;
    if (target && typeof target.focus === "function") {
        target.focus({ preventScroll: true });
        return;
    }

    const fallback = dialogElement.querySelector("button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])");
    if (fallback && typeof fallback.focus === "function") {
        fallback.focus({ preventScroll: true });
        return;
    }

    if (typeof dialogElement.focus === "function") {
        dialogElement.focus({ preventScroll: true });
    }
}

export function activate(dialogElement, dotNetRef, initialFocusSelector) {
    if (!dialogElement) {
        return;
    }

    if (typeof dialogElement._adminDialogCleanup === "function") {
        dialogElement._adminDialogCleanup();
    }

    const onKeyDown = (event) => {
        if (event.key !== "Escape") {
            return;
        }

        event.preventDefault();
        dotNetRef.invokeMethodAsync("CloseFromJs");
    };

    dialogElement.addEventListener("keydown", onKeyDown);
    dialogElement._adminDialogCleanup = () => {
        dialogElement.removeEventListener("keydown", onKeyDown);
        delete dialogElement._adminDialogCleanup;
    };

    focusInitialField(dialogElement, initialFocusSelector);
}
