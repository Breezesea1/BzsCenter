const defaultMinWidth = 72;

function getStorageKey(storageKey, columnKey) {
    return `${storageKey}:${columnKey}`;
}

function parseStoredWidth(storageKey, columnKey) {
    try {
        const raw = window.localStorage.getItem(getStorageKey(storageKey, columnKey));
        const parsed = Number.parseFloat(raw ?? "");
        return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
    }
    catch {
        return null;
    }
}

function persistWidth(storageKey, columnKey, width) {
    try {
        window.localStorage.setItem(getStorageKey(storageKey, columnKey), `${Math.round(width)}`);
    }
    catch {
    }
}

function getColumnMinWidth(header) {
    const configured = Number.parseFloat(header.dataset.minWidth ?? "");
    return Number.isFinite(configured) && configured > 0 ? configured : defaultMinWidth;
}

function setColumnWidth(col, width) {
    const px = `${Math.round(width)}px`;
    col.style.width = px;
    col.style.minWidth = px;
}

function measureColumnContentWidth(table, header, columnIndex) {
    const headerContent = header.querySelector(".admin-sort-btn, .admin-column-label") ?? header;
    const headerWidth = headerContent.scrollWidth + 24;
    const rowCells = Array.from(table.tBodies[0]?.rows ?? [])
        .slice(0, 12)
        .map((row) => row.cells[columnIndex])
        .filter(Boolean);

    let bodyWidth = 0;
    for (const cell of rowCells) {
        const measurable = cell.firstElementChild ?? cell;
        bodyWidth = Math.max(bodyWidth, measurable.scrollWidth + 16);
    }

    return Math.max(headerWidth, bodyWidth);
}

function applyInitialWidths(table, storageKey) {
    const columns = Array.from(table.querySelectorAll("col[data-column-key]"));
    const headers = Array.from(table.querySelectorAll("th[data-column-key]"));
    let restoredAny = false;

    for (const column of columns) {
        const columnKey = column.dataset.columnKey;
        if (!columnKey) {
            continue;
        }

        const restored = parseStoredWidth(storageKey, columnKey);
        if (restored) {
            setColumnWidth(column, restored);
            restoredAny = true;
        }
    }

    if (restoredAny) {
        return;
    }

    requestAnimationFrame(() => {
        headers.forEach((header, columnIndex) => {
            const columnKey = header.dataset.columnKey;
            if (!columnKey) {
                return;
            }

            const column = table.querySelector(`col[data-column-key="${columnKey}"]`);
            if (!column) {
                return;
            }

            const minWidth = getColumnMinWidth(header);
            const measured = Math.max(minWidth, Math.ceil(measureColumnContentWidth(table, header, columnIndex)));
            setColumnWidth(column, measured);
        });
    });
}

export function activate(hostElement, storageKey) {
    if (!hostElement) {
        return;
    }

    if (typeof hostElement._adminResizableCleanup === "function") {
        hostElement._adminResizableCleanup();
    }

    const table = hostElement.querySelector(".admin-table");
    if (!table) {
        return;
    }

    applyInitialWidths(table, storageKey);

    const cleanupCallbacks = [];
    const handles = Array.from(table.querySelectorAll(".admin-col-resizer[data-column-key]"));

    for (const handle of handles) {
        const columnKey = handle.dataset.columnKey;
        if (!columnKey) {
            continue;
        }

        const header = table.querySelector(`th[data-column-key="${columnKey}"]`);
        const column = table.querySelector(`col[data-column-key="${columnKey}"]`);
        if (!header || !column) {
            continue;
        }

        const minWidth = getColumnMinWidth(header);

        const updateWidth = (width) => {
            const nextWidth = Math.max(minWidth, width);
            setColumnWidth(column, nextWidth);
            persistWidth(storageKey, columnKey, nextWidth);
        };

        const onPointerDown = (event) => {
            event.preventDefault();

            const pointerId = event.pointerId;
            const startX = event.clientX;
            const startWidth = column.getBoundingClientRect().width || header.getBoundingClientRect().width;

            hostElement.classList.add("is-resizing");
            if (typeof handle.setPointerCapture === "function") {
                handle.setPointerCapture(pointerId);
            }

            const onPointerMove = (moveEvent) => {
                updateWidth(startWidth + (moveEvent.clientX - startX));
            };

            const stopResize = () => {
                hostElement.classList.remove("is-resizing");
                handle.removeEventListener("pointermove", onPointerMove);
                handle.removeEventListener("pointerup", stopResize);
                handle.removeEventListener("pointercancel", stopResize);
                if (typeof handle.releasePointerCapture === "function") {
                    try {
                        handle.releasePointerCapture(pointerId);
                    }
                    catch {
                    }
                }
            };

            handle.addEventListener("pointermove", onPointerMove);
            handle.addEventListener("pointerup", stopResize);
            handle.addEventListener("pointercancel", stopResize);
        };

        const onKeyDown = (event) => {
            const step = event.shiftKey ? 32 : 16;
            const currentWidth = column.getBoundingClientRect().width || header.getBoundingClientRect().width;

            if (event.key === "ArrowLeft") {
                event.preventDefault();
                updateWidth(currentWidth - step);
            }

            if (event.key === "ArrowRight") {
                event.preventDefault();
                updateWidth(currentWidth + step);
            }
        };

        handle.addEventListener("pointerdown", onPointerDown);
        handle.addEventListener("keydown", onKeyDown);

        cleanupCallbacks.push(() => {
            handle.removeEventListener("pointerdown", onPointerDown);
            handle.removeEventListener("keydown", onKeyDown);
        });
    }

    hostElement._adminResizableCleanup = () => {
        for (const cleanup of cleanupCallbacks) {
            cleanup();
        }

        hostElement.classList.remove("is-resizing");
        delete hostElement._adminResizableCleanup;
    };
}

export function dispose(hostElement) {
    if (hostElement && typeof hostElement._adminResizableCleanup === "function") {
        hostElement._adminResizableCleanup();
    }
}
