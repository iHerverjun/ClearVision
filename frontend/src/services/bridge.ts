import { BridgeMessageType, type BridgeMessage, type RequestRecord } from './bridge.types';
import { mockBridge } from './bridge.mock';

type MessageHandler = (message: any) => void | Promise<any>;
type ConnectionStateHandler = (isConnected: boolean) => void;

class WebMessageBridge {
  private messageHandlers = new Map<string, MessageHandler>();
  private pendingRequests = new Map<number, RequestRecord>();
  private requestId = 0;
  private isMockMode = false;
  private isConnected = false;
  private connectionStateHandlers = new Set<ConnectionStateHandler>();

  constructor() {
    this.initialize();
  }

  private setConnectionState(nextState: boolean) {
    if (this.isConnected === nextState) {
      return;
    }

    this.isConnected = nextState;
    this.connectionStateHandlers.forEach((handler) => {
      try {
        handler(nextState);
      } catch (error) {
        console.error('[WebMessageBridge] Error in connection state handler:', error);
      }
    });
  }

  private initialize() {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = window as any;
    if (w.chrome && w.chrome.webview) {
      w.chrome.webview.addEventListener('message', this.handleMessage.bind(this));
      w.chrome.webview.addEventListener('sharedbufferreceived', this.handleSharedBuffer.bind(this));
      console.log('[WebMessageBridge] WebView2 Environment Initialized');
      this.setConnectionState(true);
    } else {
      console.warn('[WebMessageBridge] Not in WebView2, using Mock mode');
      this.isMockMode = true;
      mockBridge.initialize(this.handleMessage.bind(this));
      this.setConnectionState(true);
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private handleSharedBuffer(event: any) {
    try {
      if (event.additionalData) {
        const metadata = JSON.parse(event.additionalData);
        const buffer = event.getBuffer();
        const handler = this.messageHandlers.get(BridgeMessageType.ImageStreamShared);
        if (handler) {
          handler({
            buffer,
            width: metadata.width,
            height: metadata.height,
          });
        }
      }
    } catch (error) {
      console.error('[WebMessageBridge] Error handling shared buffer:', error);
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private handleMessage(event: any) {
    const message: BridgeMessage = event?.data ?? event;
    if (!message) {
      return;
    }

    // Handle responses for pending requests
    if (message.requestId && this.pendingRequests.has(message.requestId)) {
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      const { resolve, reject } = this.pendingRequests.get(message.requestId)!;
      this.pendingRequests.delete(message.requestId);

      if (message.error) {
        reject(new Error(message.error));
      } else {
        // Backend might wrap data or spread it
        resolve(message.data !== undefined ? message.data : message);
      }
      return;
    }

    // Safely get message type to accommodate variable backend naming
    const messageType = message.type || message.messageType || message.MessageType;
    if (!messageType) {
      console.warn('[WebMessageBridge] Received message without message type:', message);
      return;
    }

    // Handle commands/events
    const handler = this.messageHandlers.get(messageType);
    if (handler) {
      try {
        const result = handler(message);
        if (result instanceof Promise) {
          result.then(res => {
             if (message.requestId) this.sendResponse(message.requestId, res);
          }).catch(err => {
             if (message.requestId) this.sendError(message.requestId, err.message);
          });
        } else {
           if (message.requestId) {
              this.sendResponse(message.requestId, result);
           }
        }
      } catch (error: any) {
        console.error('[WebMessageBridge] Error handling message:', error);
        if (message.requestId) {
          this.sendError(message.requestId, error.message || '未知错误');
        }
      }
    } else {
      console.warn('[WebMessageBridge] No handler for:', messageType);
    }
  }

  public async sendMessage<T = any>(
    type: string,
    data: any = null,
    expectResponse = false,
    timeoutMs = 15000,
  ): Promise<T> {
    const message: BridgeMessage = {
      ...data,
      messageType: type,
      timestamp: new Date().toISOString()
    };

    if (expectResponse) {
      message.requestId = ++this.requestId;
      const effectiveTimeoutMs = Number.isFinite(timeoutMs) && timeoutMs > 0 ? timeoutMs : 15000;

      return new Promise<T>((resolve, reject) => {
        const timeoutId = window.setTimeout(() => {
          if (this.pendingRequests.has(message.requestId as number)) {
            this.pendingRequests.delete(message.requestId as number);
            reject(new Error(`请求超时（${effectiveTimeoutMs}ms）：${type}`));
          }
        }, effectiveTimeoutMs);

        this.pendingRequests.set(message.requestId as number, {
          resolve: (value: any) => {
            window.clearTimeout(timeoutId);
            resolve(value);
          },
          reject: (reason?: any) => {
            window.clearTimeout(timeoutId);
            reject(reason);
          },
        });

        try {
          this.postMessage(message);
        } catch (error) {
          window.clearTimeout(timeoutId);
          this.pendingRequests.delete(message.requestId as number);
          reject(error);
        }
      });
    } else {
      this.postMessage(message);
      return Promise.resolve() as Promise<T>;
    }
  }

  private postMessage(message: BridgeMessage) {
    if (this.isMockMode) {
      mockBridge.handleFromFrontend(message);
    } else {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const w = window as any;
      if (w.chrome && w.chrome.webview) {
        try {
          w.chrome.webview.postMessage(message);
          this.setConnectionState(true);
        } catch (error) {
          this.setConnectionState(false);
          throw error;
        }
      } else {
        this.setConnectionState(false);
      }
    }
  }

  private sendResponse(requestId: number, data: any) {
    this.postMessage({
      type: 'response',
      requestId,
      data,
      timestamp: new Date().toISOString()
    });
  }

  private sendError(requestId: number, error: string) {
    this.postMessage({
      type: 'response',
      requestId,
      error,
      timestamp: new Date().toISOString()
    });
  }

  public on(type: string, handler: MessageHandler) {
    this.messageHandlers.set(type, handler);
  }

  public off(type: string) {
    this.messageHandlers.delete(type);
  }

  public getConnectionState() {
    return this.isConnected;
  }

  public isUsingMockMode() {
    return this.isMockMode;
  }

  public onConnectionStateChange(handler: ConnectionStateHandler) {
    this.connectionStateHandlers.add(handler);
    return () => this.connectionStateHandlers.delete(handler);
  }
}

export const webMessageBridge = new WebMessageBridge();
