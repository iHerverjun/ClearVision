import type { BridgeMessage } from './bridge.types';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type MockMessageHandler = (event: any) => void;

interface MockProject {
  id: string;
  name: string;
  description?: string;
  type: string;
  updatedAt: string;
}

interface MockResultRecord {
  id: string;
  projectId: string;
  status: 'OK' | 'NG' | 'Error';
  processingTimeMs: number;
  inspectionTime: string;
  outputImage?: string;
  defects: Array<{
    id: string;
    type: string;
    x: number;
    y: number;
    width: number;
    height: number;
    confidenceScore: number;
    description?: string;
  }>;
  outputData?: Record<string, unknown>;
}

const buildSvgPlaceholder = (label: string, accent = '#e84855') => {
  const escaped = label.replace(/[<>&'"]/g, '');
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="640" height="400" viewBox="0 0 640 400"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0%" stop-color="#1f2937"/><stop offset="100%" stop-color="#0f172a"/></linearGradient></defs><rect width="640" height="400" fill="url(#g)"/><rect x="20" y="20" width="600" height="360" rx="12" fill="none" stroke="${accent}" stroke-opacity="0.45" stroke-width="2"/><text x="50%" y="52%" dominant-baseline="middle" text-anchor="middle" fill="#f8fafc" font-family="Segoe UI,Arial,sans-serif" font-size="24">${escaped}</text></svg>`;
  return `data:image/svg+xml;base64,${btoa(unescape(encodeURIComponent(svg)))}`;
};

class MockBridge {
  private frontendHandler: MockMessageHandler | null = null;

  private mockProjects: MockProject[] = [
    {
      id: 'proj-1001',
      name: '视觉检测线_01',
      description: 'PCB AOI 产线',
      type: 'PCB',
      updatedAt: new Date(Date.now() - 10 * 60 * 1000).toISOString(),
    },
    {
      id: 'proj-1002',
      name: '标签识别终版',
      description: 'OCR + 测量',
      type: 'OCR',
      updatedAt: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(),
    },
  ];

  private mockResults: MockResultRecord[] = [
    {
      id: 'res-5001',
      projectId: 'proj-1001',
      status: 'NG',
      processingTimeMs: 42,
      inspectionTime: new Date(Date.now() - 20 * 60 * 1000).toISOString(),
      outputImage: buildSvgPlaceholder('检测 #5001', '#ef4444'),
      defects: [
        {
          id: 'def-1',
          type: '划痕',
          x: 180,
          y: 110,
          width: 120,
          height: 80,
          confidenceScore: 0.93,
          description: '表面划痕',
        },
      ],
      outputData: {
        serialNumber: 'SN-20260222-5001',
        score: 87.5,
        ocrText: 'L88-421-B',
      },
    },
    {
      id: 'res-5002',
      projectId: 'proj-1001',
      status: 'OK',
      processingTimeMs: 39,
      inspectionTime: new Date(Date.now() - 35 * 60 * 1000).toISOString(),
      outputImage: buildSvgPlaceholder('检测 #5002', '#22c55e'),
      defects: [],
      outputData: {
        serialNumber: 'SN-20260222-5002',
        score: 99.1,
      },
    },
    {
      id: 'res-5003',
      projectId: 'proj-1002',
      status: 'OK',
      processingTimeMs: 46,
      inspectionTime: new Date(Date.now() - 75 * 60 * 1000).toISOString(),
      outputImage: buildSvgPlaceholder('检测 #5003', '#22c55e'),
      defects: [],
      outputData: {
        serialNumber: 'SN-20260222-5003',
        ocrText: 'QR-7781-A',
      },
    },
  ];

  public initialize(handler: MockMessageHandler) {
    this.frontendHandler = handler;
  }

  public handleFromFrontend(message: BridgeMessage) {
    console.log('[MockBridge] Received from frontend:', message);

    const type = message.messageType || message.type;

    setTimeout(() => {
      this.dispatchResponse(message, type);
    }, 180);
  }

  private dispatchResponse(message: BridgeMessage, type?: string) {
    let mockData: Record<string, unknown> | null = null;

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
            Version: '1.0.0',
            Nodes: [
              {
                Id: '1',
                Name: '采集',
                Type: 'ImageAcquisition',
                Location: { X: 100, Y: 100 },
                InputPorts: [],
                OutputPorts: [{ Id: '1-o1', Name: 'Image', Description: '', DataType: 'Image' }],
                Configuration: {},
              },
              {
                Id: '2',
                Name: '边缘检测',
                Type: 'CannyEdge',
                Location: { X: 340, Y: 100 },
                InputPorts: [{ Id: '2-i1', Name: 'Image', Description: '', DataType: 'Image' }],
                OutputPorts: [{ Id: '2-o1', Name: 'Image', Description: '', DataType: 'Image' }],
                Configuration: {},
              },
            ],
            Edges: [
              {
                Id: 'e1-2',
                SourceOperatorId: '1',
                SourcePortId: '1-o1',
                TargetOperatorId: '2',
                TargetPortId: '2-i1',
              },
            ],
          }),
          usedTools: ['ImageAcquisition', 'CannyEdge'],
          rawResponse: '已生成两步流程。',
        };
        break;
      case 'settings.get':
        mockData = {
          settings: {
            general: { theme: 'system', language: 'zh-CN', autoSaveInterval: 5 },
            camera: { defaultResolution: '1920x1080', exposureTarget: 120 },
            communication: { protocol: 'TCP', host: '127.0.0.1', port: 8080 },
            ai: { apiKey: 'mock-key-123', model: 'DeepSeek-V3', timeoutMs: 30000 },
          },
        };
        break;
      case 'settings.save':
        mockData = { success: true };
        break;
      case 'CalibSolveCommand':
      case 'HandEyeSolveCommand':
        mockData = {
          success: true,
          error: 0.045,
          matrix: [
            [1, 0, 0],
            [0, 1, 0],
            [0, 0, 1],
          ],
        };
        break;
      case 'CalibSaveCommand':
      case 'HandEyeSaveCommand':
        mockData = { success: true };
        break;
      case 'ProjectListQuery':
        mockData = { projects: this.mockProjects };
        break;
      case 'ProjectCreateCommand': {
        const created: MockProject = {
          id: `proj-${Date.now()}`,
          name: String(message.name || `工程_${this.mockProjects.length + 1}`),
          description: typeof message.description === 'string' ? message.description : '',
          type: String(message.type || '通用'),
          updatedAt: new Date().toISOString(),
        };
        this.mockProjects = [created, ...this.mockProjects];
        mockData = { success: true, project: created, projectId: created.id };
        break;
      }
      case 'ProjectDeleteCommand': {
        const projectId = String(message.projectId || message.id || '');
        this.mockProjects = this.mockProjects.filter((project) => project.id !== projectId);
        this.mockResults = this.mockResults.filter((result) => result.projectId !== projectId);
        mockData = { success: true, projectId };
        break;
      }
      case 'ProjectOpenCommand': {
        const projectId = String(message.projectId || '');
        const project = this.mockProjects.find((item) => item.id === projectId) || null;
        mockData = { success: true, projectId, project };
        break;
      }
      case 'ResultsQuery': {
        const projectId = typeof message.projectId === 'string' ? message.projectId : undefined;
        const status = typeof message.status === 'string' ? message.status.toUpperCase() : 'ALL';
        const keyword = typeof message.search === 'string' ? message.search.trim().toLowerCase() : '';

        const records = this.mockResults.filter((record) => {
          if (projectId && record.projectId !== projectId) return false;
          if (status === 'OK' || status === 'NG' || status === 'ERROR') {
            if (record.status !== status) return false;
          }
          if (keyword) {
            const serial = String(record.outputData?.serialNumber || '').toLowerCase();
            if (!serial.includes(keyword) && !record.id.toLowerCase().includes(keyword)) {
              return false;
            }
          }
          return true;
        });

        mockData = {
          records: records.sort(
            (a, b) => new Date(b.inspectionTime).getTime() - new Date(a.inspectionTime).getTime(),
          ),
        };
        break;
      }
      case 'ResultsExportCommand':
        mockData = {
          success: true,
          fileName: `inspection-report-${message.recordId || 'unknown'}.json`,
          content: JSON.stringify(
            this.mockResults.find((record) => record.id === message.recordId) || {},
            null,
            2,
          ),
        };
        break;
      case 'FalsePositiveCommand':
        mockData = { success: true, recordId: message.recordId };
        break;
      case 'SystemStatsQuery': {
        const total = this.mockResults.length;
        const okCount = this.mockResults.filter((record) => record.status === 'OK').length;
        const avgMs = total > 0
          ? Math.round(
              this.mockResults.reduce((sum, record) => sum + record.processingTimeMs, 0) / total,
            )
          : 0;
        mockData = {
          averageYield: total > 0 ? Number(((okCount / total) * 100).toFixed(1)) : 0,
          averageCycleTimeMs: avgMs,
          storageUsedGb: 1.2,
          totalInspections: total,
          okCount,
          ngCount: total - okCount,
        };
        break;
      }
      case 'HardwareStatusQuery':
        mockData = {
          cpuUsage: 14,
          memoryUsedGb: 1.3,
          memoryTotalGb: 16,
          isBridgeConnected: true,
          cameraStatus: 'connected',
          runningInspections: 0,
        };
        break;
      case 'ActivityLogQuery':
        mockData = {
          activities: this.mockResults.slice(0, 10).map((record) => ({
            id: `act-${record.id}`,
            title: `检测 ${record.status}`,
            description: `结果 ${record.id}`,
            timestamp: record.inspectionTime,
            type: 'InspectionCompleted',
            userName: '操作员',
          })),
        };
        break;
      default:
        mockData = { success: true, mockMode: true };
        break;
    }

    if (message.requestId && this.frontendHandler) {
      this.frontendHandler({
        data: {
          requestId: message.requestId,
          data: mockData,
        },
      });
    }
  }

  // Can be used to simulate proactive backend events
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  public simulateBackendEvent(type: string, data: any) {
    if (this.frontendHandler) {
      this.frontendHandler({
        data: {
          messageType: type,
          data,
        },
      });
    }
  }
}

export const mockBridge = new MockBridge();
