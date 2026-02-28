/**
 * WebMessage bridge for WebView2 host communication.
 */
class WebMessageBridge {
    constructor() {
        // messageType -> Set<handler>
        this.messageHandlers = new Map();
        this.pendingRequests = new Map();
        this.requestId = 0;
        this.mockMode = false;

        this.initialize();
    }

    initialize() {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.addEventListener('message', this.handleMessage.bind(this));
            window.chrome.webview.addEventListener('sharedbufferreceived', this.handleSharedBuffer.bind(this));
            console.log('[WebMessageBridge] Initialized in WebView2');
        } else {
            console.warn('[WebMessageBridge] Not in WebView2, using mock mode');
            this.enableMockMode();
        }
    }

    handleSharedBuffer(event) {
        try {
            if (!event.additionalData) {
                return;
            }

            const metadata = JSON.parse(event.additionalData);
            const payload = {
                buffer: event.getBuffer(),
                width: metadata.width,
                height: metadata.height
            };

            const handlers = this.messageHandlers.get('image.stream.shared');
            if (!handlers || handlers.size === 0) {
                return;
            }

            handlers.forEach((handler) => {
                try {
                    handler(payload);
                } catch (error) {
                    console.error('[WebMessageBridge] Shared buffer handler failed:', error);
                }
            });
        } catch (error) {
            console.error('[WebMessageBridge] Failed to process shared buffer:', error);
        }
    }

    enableMockMode() {
        this.mockMode = true;
        window.mockWebViewResponse = (message) => {
            this.handleMessage({ data: message });
        };
    }

    handleMessage(event) {
        const message = event?.data;
        const messageType = message ? (message.type || message.messageType || message.MessageType) : null;

        if (!message || !messageType) {
            console.warn('[WebMessageBridge] Invalid message:', message);
            return;
        }

        console.log('[WebMessageBridge] Received message:', messageType, message);

        if (message.requestId && this.pendingRequests.has(message.requestId)) {
            const { resolve, reject } = this.pendingRequests.get(message.requestId);
            this.pendingRequests.delete(message.requestId);

            if (message.error) {
                reject(new Error(message.error));
            } else {
                resolve(message.data ?? message.payload ?? message);
            }
            return;
        }

        const handlers = this.messageHandlers.get(messageType);
        if (!handlers || handlers.size === 0) {
            console.warn('[WebMessageBridge] No handler for message type:', messageType);
            return;
        }

        let firstResult;
        let hasResult = false;
        let firstError = null;

        handlers.forEach((handler) => {
            try {
                const result = handler(message);
                if (!hasResult) {
                    firstResult = result;
                    hasResult = true;
                }
            } catch (error) {
                if (!firstError) {
                    firstError = error;
                }
                console.error('[WebMessageBridge] Handler failed:', error);
            }
        });

        if (message.requestId) {
            if (firstError) {
                this.sendError(message.requestId, firstError.message || 'Unknown handler error');
            } else {
                this.sendResponse(message.requestId, firstResult);
            }
        }
    }

    async sendMessage(type, data = null, expectResponse = false) {
        const message = {
            ...(data || {}),
            messageType: type,
            timestamp: new Date().toISOString()
        };

        if (expectResponse) {
            message.requestId = ++this.requestId;

            return new Promise((resolve, reject) => {
                this.pendingRequests.set(message.requestId, { resolve, reject });

                setTimeout(() => {
                    if (this.pendingRequests.has(message.requestId)) {
                        this.pendingRequests.delete(message.requestId);
                        reject(new Error('Request timeout'));
                    }
                }, 30000);

                this.postMessage(message);
            });
        }

        this.postMessage(message);
        return Promise.resolve();
    }

    postMessage(message) {
        if (this.mockMode) {
            console.log('[WebMessageBridge] Mock post:', message);
            return;
        }

        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(message);
            return;
        }

        console.error('[WebMessageBridge] Unable to post message, WebView2 unavailable');
    }

    sendResponse(requestId, data) {
        this.postMessage({
            type: 'response',
            requestId,
            data,
            timestamp: Date.now()
        });
    }

    sendError(requestId, error) {
        this.postMessage({
            type: 'response',
            requestId,
            error,
            timestamp: Date.now()
        });
    }

    on(type, handler) {
        if (!type || typeof handler !== 'function') {
            return () => {};
        }

        let handlers = this.messageHandlers.get(type);
        if (!handlers) {
            handlers = new Set();
            this.messageHandlers.set(type, handlers);
        }

        handlers.add(handler);
        return () => this.off(type, handler);
    }

    off(type, handler = null) {
        const handlers = this.messageHandlers.get(type);
        if (!handlers) {
            return;
        }

        if (!handler) {
            this.messageHandlers.delete(type);
            return;
        }

        handlers.delete(handler);
        if (handlers.size === 0) {
            this.messageHandlers.delete(type);
        }
    }
}

const webMessageBridge = new WebMessageBridge();

export default webMessageBridge;
export { WebMessageBridge };