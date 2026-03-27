export const PROPERTY_SIDEBAR_STORAGE_KEY = 'cv_flow_property_sidebar_width';
export const PROPERTY_SIDEBAR_DEFAULT_WIDTH = 280;
export const PROPERTY_SIDEBAR_MIN_WIDTH = 240;
export const PROPERTY_SIDEBAR_MAX_WIDTH = 560;
export const PROPERTY_SIDEBAR_DESKTOP_BREAKPOINT = 768;

const PROPERTY_SIDEBAR_MAX_VIEWPORT_RATIO = 0.45;
const DRAGGING_BODY_CLASS = 'property-sidebar-resizing';
const DRAGGING_HANDLE_CLASS = 'is-dragging';
const KEYBOARD_STEP = 16;
const KEYBOARD_STEP_LARGE = 32;

function getGlobalWindow() {
    return typeof window !== 'undefined' ? window : null;
}

function getDocumentRoot() {
    return typeof document !== 'undefined' ? document.documentElement : null;
}

function getDocumentBody() {
    return typeof document !== 'undefined' ? document.body : null;
}

function resolveElement(target) {
    if (!target) {
        return null;
    }

    if (typeof target === 'string') {
        return typeof document !== 'undefined' ? document.querySelector(target) : null;
    }

    return target;
}

function getStorage() {
    try {
        return window.localStorage;
    } catch {
        return null;
    }
}

function getViewportWidth(viewportWidth) {
    const numericWidth = Number(viewportWidth);
    if (Number.isFinite(numericWidth) && numericWidth > 0) {
        return numericWidth;
    }

    return getGlobalWindow()?.innerWidth || 1280;
}

function readStoredWidthCandidate(storage = getStorage()) {
    if (!storage || typeof storage.getItem !== 'function') {
        return PROPERTY_SIDEBAR_DEFAULT_WIDTH;
    }

    try {
        const rawValue = storage.getItem(PROPERTY_SIDEBAR_STORAGE_KEY);
        if (rawValue == null || rawValue === '') {
            return PROPERTY_SIDEBAR_DEFAULT_WIDTH;
        }

        const parsedWidth = Number(rawValue);
        if (!Number.isFinite(parsedWidth)) {
            return PROPERTY_SIDEBAR_DEFAULT_WIDTH;
        }

        const normalizedWidth = Math.round(parsedWidth);
        if (normalizedWidth < PROPERTY_SIDEBAR_MIN_WIDTH || normalizedWidth > PROPERTY_SIDEBAR_MAX_WIDTH) {
            return PROPERTY_SIDEBAR_DEFAULT_WIDTH;
        }

        return normalizedWidth;
    } catch {
        return PROPERTY_SIDEBAR_DEFAULT_WIDTH;
    }
}

export function getMaxWidth(viewportWidth = getViewportWidth()) {
    return Math.min(
        PROPERTY_SIDEBAR_MAX_WIDTH,
        Math.round(getViewportWidth(viewportWidth) * PROPERTY_SIDEBAR_MAX_VIEWPORT_RATIO)
    );
}

export function clampWidth(width, viewportWidth = getViewportWidth()) {
    const parsedWidth = Number(width);
    const safeWidth = Number.isFinite(parsedWidth)
        ? Math.round(parsedWidth)
        : PROPERTY_SIDEBAR_DEFAULT_WIDTH;

    return Math.min(
        getMaxWidth(viewportWidth),
        Math.max(PROPERTY_SIDEBAR_MIN_WIDTH, safeWidth)
    );
}

export function readSavedWidth({
    storage = getStorage(),
    viewportWidth = getViewportWidth()
} = {}) {
    return clampWidth(readStoredWidthCandidate(storage), viewportWidth);
}

export function applyWidth({
    root = getDocumentRoot(),
    handle = null,
    width = PROPERTY_SIDEBAR_DEFAULT_WIDTH,
    viewportWidth = getViewportWidth()
} = {}) {
    const nextWidth = clampWidth(width, viewportWidth);

    if (root?.style?.setProperty) {
        root.style.setProperty('--right-sidebar-width', `${nextWidth}px`);
    }

    if (handle?.setAttribute) {
        handle.setAttribute('aria-valuemin', String(PROPERTY_SIDEBAR_MIN_WIDTH));
        handle.setAttribute('aria-valuemax', String(getMaxWidth(viewportWidth)));
        handle.setAttribute('aria-valuenow', String(nextWidth));
    }

    return nextWidth;
}

export class PropertySidebarController {
    constructor({
        handle,
        root = getDocumentRoot(),
        storage = getStorage(),
        getCurrentView = () => 'flow'
    } = {}) {
        this.handle = resolveElement(handle);
        this.root = root;
        this.storage = storage;
        this.getCurrentView = typeof getCurrentView === 'function'
            ? getCurrentView
            : () => 'flow';

        this.currentWidth = null;
        this.preferredWidth = readStoredWidthCandidate(this.storage);
        this.dragState = null;

        this.handlePointerDown = this.handlePointerDown.bind(this);
        this.handlePointerMove = this.handlePointerMove.bind(this);
        this.handlePointerUp = this.handlePointerUp.bind(this);
        this.handleKeyDown = this.handleKeyDown.bind(this);
        this.handleWindowResize = this.handleWindowResize.bind(this);

        this.handle?.addEventListener('pointerdown', this.handlePointerDown);
        this.handle?.addEventListener('keydown', this.handleKeyDown);
        getGlobalWindow()?.addEventListener('resize', this.handleWindowResize);

        this.sync();
    }

    getViewportWidth() {
        return getViewportWidth();
    }

    isDesktopViewport() {
        return this.getViewportWidth() > PROPERTY_SIDEBAR_DESKTOP_BREAKPOINT;
    }

    isEnabled(view = this.getCurrentView()) {
        return view === 'flow' && this.isDesktopViewport();
    }

    sync(view = this.getCurrentView()) {
        const enabled = this.isEnabled(view);

        if (this.handle) {
            this.handle.classList.toggle('hidden', !enabled);
            this.handle.setAttribute('aria-disabled', enabled ? 'false' : 'true');
            this.handle.setAttribute('tabindex', enabled ? '0' : '-1');

            if (enabled) {
                this.handle.removeAttribute('aria-hidden');
            } else {
                this.handle.setAttribute('aria-hidden', 'true');
            }
        }

        if (!enabled) {
            this.stopDragging({ persist: false });
            return this.currentWidth;
        }

        this.currentWidth = applyWidth({
            root: this.root,
            handle: this.handle,
            width: this.preferredWidth,
            viewportWidth: this.getViewportWidth()
        });

        return this.currentWidth;
    }

    handlePointerDown(event) {
        if (!this.isEnabled()) {
            return;
        }

        if (!event.isPrimary || event.button !== 0) {
            return;
        }

        event.preventDefault();

        const baseWidth = this.currentWidth ?? readSavedWidth({
            storage: this.storage,
            viewportWidth: this.getViewportWidth()
        });

        this.dragState = {
            pointerId: event.pointerId,
            startX: event.clientX,
            startWidth: baseWidth
        };

        try {
            this.handle?.setPointerCapture?.(event.pointerId);
        } catch {
            // Synthetic pointer events in tests may not support pointer capture.
        }
        this.handle?.classList.add(DRAGGING_HANDLE_CLASS);
        getDocumentBody()?.classList.add(DRAGGING_BODY_CLASS);

        const globalWindow = getGlobalWindow();
        globalWindow?.addEventListener('pointermove', this.handlePointerMove);
        globalWindow?.addEventListener('pointerup', this.handlePointerUp);
        globalWindow?.addEventListener('pointercancel', this.handlePointerUp);
    }

    handlePointerMove(event) {
        if (!this.dragState || event.pointerId !== this.dragState.pointerId) {
            return;
        }

        event.preventDefault();

        const deltaX = this.dragState.startX - event.clientX;
        this.currentWidth = applyWidth({
            root: this.root,
            handle: this.handle,
            width: this.dragState.startWidth + deltaX,
            viewportWidth: this.getViewportWidth()
        });
    }

    handlePointerUp(event) {
        if (!this.dragState || event.pointerId !== this.dragState.pointerId) {
            return;
        }

        this.stopDragging({
            persist: this.isEnabled(),
            width: this.currentWidth ?? this.dragState.startWidth,
            pointerId: event.pointerId
        });
    }

    handleKeyDown(event) {
        if (!this.isEnabled()) {
            return;
        }

        const step = event.shiftKey ? KEYBOARD_STEP_LARGE : KEYBOARD_STEP;
        let nextWidth = null;

        switch (event.key) {
            case 'ArrowLeft':
                nextWidth = (this.currentWidth ?? this.preferredWidth) + step;
                break;
            case 'ArrowRight':
                nextWidth = (this.currentWidth ?? this.preferredWidth) - step;
                break;
            case 'Home':
                nextWidth = PROPERTY_SIDEBAR_MIN_WIDTH;
                break;
            case 'End':
                nextWidth = getMaxWidth(this.getViewportWidth());
                break;
            default:
                return;
        }

        event.preventDefault();
        this.commitWidth(nextWidth);
    }

    commitWidth(width) {
        const nextWidth = clampWidth(width, this.getViewportWidth());
        this.preferredWidth = nextWidth;
        this.currentWidth = applyWidth({
            root: this.root,
            handle: this.handle,
            width: nextWidth,
            viewportWidth: this.getViewportWidth()
        });

        try {
            this.storage?.setItem(PROPERTY_SIDEBAR_STORAGE_KEY, String(nextWidth));
        } catch {
            // Ignore storage failures and keep runtime width.
        }

        return this.currentWidth;
    }

    stopDragging({
        persist = false,
        width = this.currentWidth,
        pointerId = this.dragState?.pointerId
    } = {}) {
        if (pointerId != null) {
            try {
                this.handle?.releasePointerCapture?.(pointerId);
            } catch {
                // Ignore missing pointer capture on synthetic events.
            }
        }

        const globalWindow = getGlobalWindow();
        globalWindow?.removeEventListener('pointermove', this.handlePointerMove);
        globalWindow?.removeEventListener('pointerup', this.handlePointerUp);
        globalWindow?.removeEventListener('pointercancel', this.handlePointerUp);

        this.dragState = null;
        this.handle?.classList.remove(DRAGGING_HANDLE_CLASS);
        getDocumentBody()?.classList.remove(DRAGGING_BODY_CLASS);

        if (persist) {
            this.commitWidth(width);
        }

        return this.currentWidth;
    }

    handleWindowResize() {
        this.sync();
    }

    destroy() {
        this.stopDragging({ persist: false });
        getGlobalWindow()?.removeEventListener('resize', this.handleWindowResize);
        this.handle?.removeEventListener('pointerdown', this.handlePointerDown);
        this.handle?.removeEventListener('keydown', this.handleKeyDown);

        if (this.handle) {
            this.handle.classList.add('hidden');
            this.handle.classList.remove(DRAGGING_HANDLE_CLASS);
            this.handle.setAttribute('aria-disabled', 'true');
            this.handle.setAttribute('aria-hidden', 'true');
            this.handle.setAttribute('tabindex', '-1');
        }
    }
}

export default PropertySidebarController;
