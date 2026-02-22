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
      case 'PickFileCommand':
        if (this.frontendHandler) {
          this.frontendHandler({
            data: {
              messageType: 'FilePickedEvent',
              parameterName: message.parameterName ?? 'filePath',
              filePath: 'C:\\test\\image.jpg',
              previewImageBase64:
                'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO6n5L8AAAAASUVORK5CYII=',
              isCancelled: false,
            },
          });
        }
        break;
      case 'GenerateFlowCommand':
        mockData = {
          success: true,
          flowJson: JSON.stringify({
            nodes: [
              { id: '1', type: 'ImageSourceOperator', position: { x: 100, y: 100 }, data: { name: 'Capture' } },
              { id: '2', type: 'EdgeDetectionOperator', position: { x: 300, y: 100 }, data: { name: 'Edge Detect' } }
            ],
            edges: [
              { id: 'e1-2', source: '1', target: '2' }
            ]
          }),
          usedTools: ['ImageSourceOperator', 'EdgeDetectionOperator'],
          rawResponse: "I have generated a flow that captures an image and performs edge detection."
        };
        break;
      case 'settings.get':
        mockData = {
          settings: {
            general: { theme: 'system', language: 'zh-CN', autoSaveInterval: 5 },
            camera: { defaultResolution: '1920x1080', exposureTarget: 120 },
            communication: { protocol: 'TCP', host: '127.0.0.1', port: 8080 },
            ai: { apiKey: 'mock-key-123', model: 'DeepSeek-V3', timeoutMs: 30000 }
          }
        };
        break;
      case 'settings.save':
        mockData = { success: true };
        break;
      case 'CalibSolveCommand':
      case 'HandEyeSolveCommand':
        mockData = {
          success: true,
          error: 0.045, // reprojection error
          matrix: [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
        };
        break;
      case 'CalibSaveCommand':
      case 'HandEyeSaveCommand':
        mockData = { success: true };
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
