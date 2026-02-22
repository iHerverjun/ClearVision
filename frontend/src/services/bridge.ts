import { BridgeMessageType, type BridgeMessage, type RequestRecord } from './bridge.types';
import { mockBridge } from './bridge.mock';

type MessageHandler = (message: any) => void | Promise<any>;

class WebMessageBridge {
  private messageHandlers = new Map<string, MessageHandler>();
  private pendingRequests = new Map<number, RequestRecord>();
  private requestId = 0;
  private isMockMode = false;

  constructor() {
    this.initialize();
  }

  private initialize() {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = window as any;
    if (w.chrome && w.chrome.webview) {
      w.chrome.webview.addEventListener('message', this.handleMessage.bind(this));
      w.chrome.webview.addEventListener('sharedbufferreceived', this.handleSharedBuffer.bind(this));
      console.log('[WebMessageBridge] WebView2 Environment Initialized');
    } else {
      console.warn('[WebMessageBridge] Not in WebView2, using Mock mode');
      this.isMockMode = true;
      mockBridge.initialize(this.handleMessage.bind(this));
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
    const message: BridgeMessage = event.data;
    
    // Safely get message type to accommodate variable backend naming
    const messageType = message ? (message.type || message.messageType || message.MessageType) : null;
    
    if (!message || !messageType) {
      console.warn('[WebMessageBridge] Received invalid message:', message);
      return;
    }

    // console.log('[WebMessageBridge] Received:', messageType, message);

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
          this.sendError(message.requestId, error.message || 'Unknown error');
        }
      }
    } else {
      console.warn('[WebMessageBridge] No handler for:', messageType);
    }
  }

  public async sendMessage<T = any>(type: string, data: any = null, expectResponse = false): Promise<T> {
    const message: BridgeMessage = {
      ...data,
      messageType: type,
      timestamp: new Date().toISOString()
    };

    if (expectResponse) {
      message.requestId = ++this.requestId;
      
      return new Promise<T>((resolve, reject) => {
        this.pendingRequests.set(message.requestId as number, { resolve, reject });
        
        // Timeout 30s
        setTimeout(() => {
          if (this.pendingRequests.has(message.requestId as number)) {
            this.pendingRequests.delete(message.requestId as number);
            reject(new Error(`Request timeout for type: ${type}`));
          }
        }, 30000);

        this.postMessage(message);
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
        w.chrome.webview.postMessage(message);
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
}

export const webMessageBridge = new WebMessageBridge();
