import type { BridgeMessage } from './bridge.types';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type MockMessageHandler = (event: any) => void;

class MockBridge {
  private frontendHandler: MockMessageHandler | null = null;

  public initialize(handler: MockMessageHandler) {
    this.frontendHandler = handler;
  }

  public handleFromFrontend(message: BridgeMessage) {
    console.log('[MockBridge] Received from frontend:', message);

    const type = message.messageType || message.type;
    
    // Simulate async processing
    setTimeout(() => {
      this.dispatchResponse(message, type);
    }, 500);
  }

  private dispatchResponse(message: BridgeMessage, type?: string) {
    let mockData = null;

    switch (type) {
      case 'app.ready':
        mockData = { status: 'ok' };
        break;
      case 'dialog.selectFile':
        mockData = { filePath: 'C:\\test\\image.jpg' };
        break;
      default:
        mockData = { success: true, mockMode: true };
        break;
    }

    if (message.requestId && this.frontendHandler) {
      this.frontendHandler({
        data: {
          requestId: message.requestId,
          data: mockData
        }
      });
    }
  }

  // Can be used to simulate proactive backend events
  public simulateBackendEvent(type: string, data: any) {
    if (this.frontendHandler) {
      this.frontendHandler({
        data: {
          messageType: type,
          data
        }
      });
    }
  }
}

export const mockBridge = new MockBridge();
