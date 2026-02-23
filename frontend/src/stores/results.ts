import { defineStore } from 'pinia';
import { computed, reactive, ref } from 'vue';
import { apiClient } from '../services/api';
import { webMessageBridge } from '../services/bridge';
import { BridgeMessageType } from '../services/bridge.types';
import { useProjectsStore } from './projects';

export interface DefectRegion {
  id: string;
  type: string;
  x: number;
  y: number;
  width: number;
  height: number;
  confidenceScore: number;
  description?: string;
}

export interface InspectionRecord {
  id: string;
  projectId: string;
  status: 'OK' | 'NG' | 'Error';
  processingTimeMs: number;
  inspectionTime: string;
  outputImage?: string;
  defects: DefectRegion[];
  outputData?: Record<string, unknown>;
}

export interface FilterState {
  projectId?: string;
  startDate?: string;
  endDate?: string;
  status: 'all' | 'ok' | 'ng' | 'error';
  searchQuery: string;
}

const getPlaceholder = (label: string, color = '#334155') => {
  const safe = label.replace(/[<>&'"]/g, '');
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="960" height="540" viewBox="0 0 960 540"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0%" stop-color="#0f172a"/><stop offset="100%" stop-color="#111827"/></linearGradient></defs><rect width="960" height="540" fill="url(#g)"/><rect x="40" y="40" width="880" height="460" rx="20" fill="none" stroke="${color}" stroke-opacity="0.6" stroke-width="3"/><text x="50%" y="52%" dominant-baseline="middle" text-anchor="middle" fill="#e2e8f0" font-family="Segoe UI,Arial,sans-serif" font-size="30">${safe}</text></svg>`;
  return `data:image/svg+xml;base64,${btoa(unescape(encodeURIComponent(svg)))}`;
};

const normalizeImage = (raw: unknown, label: string) => {
  if (typeof raw !== 'string' || !raw.trim()) {
    return getPlaceholder(label);
  }

  if (raw.startsWith('data:') || raw.startsWith('http://') || raw.startsWith('https://') || raw.startsWith('file:')) {
    return raw;
  }

  return `data:image/png;base64,${raw}`;
};

const parseOutputData = (raw: any): Record<string, unknown> | undefined => {
  if (raw?.outputData && typeof raw.outputData === 'object') {
    return raw.outputData as Record<string, unknown>;
  }

  if (typeof raw?.outputDataJson === 'string' && raw.outputDataJson.trim()) {
    try {
      const parsed = JSON.parse(raw.outputDataJson);
      if (parsed && typeof parsed === 'object') {
        return parsed as Record<string, unknown>;
      }
    } catch {
      // keep undefined
    }
  }

  return undefined;
};

const normalizeRecord = (raw: any): InspectionRecord => {
  const statusRaw = String(raw?.status || 'Error').toUpperCase();
  const status: InspectionRecord['status'] =
    statusRaw === 'OK' || statusRaw === 'NG' || statusRaw === 'ERROR'
      ? (statusRaw === 'ERROR' ? 'Error' : (statusRaw as 'OK' | 'NG'))
      : 'Error';

  const id = String(raw?.id || raw?.resultId || `record-${Date.now()}`);
  const outputData = parseOutputData(raw);

  const defects: DefectRegion[] = Array.isArray(raw?.defects)
    ? raw.defects.map((defect: any, index: number) => ({
        id: String(defect?.id || `${id}-defect-${index}`),
        type: String(defect?.type || '未知'),
        x: Number(defect?.x || 0),
        y: Number(defect?.y || 0),
        width: Number(defect?.width || 0),
        height: Number(defect?.height || 0),
        confidenceScore: Number(defect?.confidenceScore || defect?.confidence || 0),
        description: typeof defect?.description === 'string' ? defect.description : '',
      }))
    : [];

  return {
    id,
    projectId: String(raw?.projectId || ''),
    status,
    processingTimeMs: Number(raw?.processingTimeMs || 0),
    inspectionTime: String(raw?.inspectionTime || new Date().toISOString()),
    outputImage: normalizeImage(raw?.outputImage, `检测 ${id}`),
    defects,
    outputData,
  };
};

const withQuery = (base: string, query: Record<string, string | undefined>) => {
  const params = new URLSearchParams();
  Object.entries(query).forEach(([key, value]) => {
    if (value && value.trim()) {
      params.set(key, value);
    }
  });
  const queryString = params.toString();
  return queryString ? `${base}?${queryString}` : base;
};

export const useResultsStore = defineStore('results', () => {
  const projectsStore = useProjectsStore();

  const records = ref<InspectionRecord[]>([]);
  const selectedRecord = ref<InspectionRecord | null>(null);
  const filters = reactive<FilterState>({
    projectId: '',
    startDate: '',
    endDate: '',
    status: 'all',
    searchQuery: '',
  });
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  const stats = computed(() => ({
    total: records.value.length,
    ngCount: records.value.filter((record) => record.status === 'NG').length,
  }));

  const filteredRecords = computed(() => {
    const keyword = filters.searchQuery.trim().toLowerCase();
    return records.value.filter((record) => {
      if (filters.status !== 'all' && record.status.toLowerCase() !== filters.status) {
        return false;
      }

      if (keyword) {
        const serial = String(record.outputData?.serialNumber || '').toLowerCase();
        if (!record.id.toLowerCase().includes(keyword) && !serial.includes(keyword)) {
          return false;
        }
      }
      return true;
    });
  });

  const ensureProject = async () => {
    if (filters.projectId) {
      return filters.projectId;
    }

    if (projectsStore.projects.length === 0) {
      await projectsStore.loadProjects();
    }

    const first = projectsStore.currentProject?.id || projectsStore.projects[0]?.id || '';
    filters.projectId = first;
    return first;
  };

  async function loadRecords(override?: Partial<FilterState>) {
    Object.assign(filters, override || {});
    isLoading.value = true;
    error.value = null;

    try {
      const bridgeResponse = await webMessageBridge.sendMessage(
        BridgeMessageType.ResultsQuery,
        {
          projectId: filters.projectId || undefined,
          startDate: filters.startDate || undefined,
          endDate: filters.endDate || undefined,
          status: filters.status,
          search: filters.searchQuery,
        },
        true,
      );

      if (Array.isArray(bridgeResponse?.records)) {
        records.value = bridgeResponse.records.map(normalizeRecord);
      } else {
        throw new Error('桥接响应缺少记录数据');
      }
    } catch {
      try {
        const projectId = await ensureProject();
        if (!projectId) {
          records.value = [];
          selectedRecord.value = null;
          return;
        }

        const url = withQuery(`/api/inspection/history/${projectId}`, {
          startTime: filters.startDate,
          endTime: filters.endDate,
          pageIndex: '0',
          pageSize: '200',
        });
        const response = await apiClient.get(url);
        records.value = (Array.isArray(response) ? response : []).map(normalizeRecord);
      } catch (apiError: any) {
        error.value = apiError?.message || '加载检测记录失败';
        records.value = [];
      }
    } finally {
      records.value.sort(
        (a, b) => new Date(b.inspectionTime).getTime() - new Date(a.inspectionTime).getTime(),
      );
      selectedRecord.value = filteredRecords.value[0] || null;
      isLoading.value = false;
    }
  }

  function selectRecord(record: InspectionRecord | null) {
    selectedRecord.value = record;
  }

  async function exportReport(recordId: string) {
    const record = records.value.find((item) => item.id === recordId);
    if (!record) {
      return;
    }

    let fileName = `inspection-report-${recordId}.json`;
    let content = JSON.stringify(record, null, 2);

    try {
      const response = await webMessageBridge.sendMessage(
        BridgeMessageType.ResultsExportCommand,
        { recordId },
        true,
      );

      if (typeof response?.fileName === 'string') {
        fileName = response.fileName;
      }
      if (typeof response?.content === 'string') {
        content = response.content;
      }
    } catch {
      // keep local fallback content
    }

    const blob = new Blob([content], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  async function markFalsePositive(recordId: string) {
    const target = records.value.find((record) => record.id === recordId);
    if (!target) {
      return;
    }

    try {
      await webMessageBridge.sendMessage(
        BridgeMessageType.FalsePositiveCommand,
        { recordId },
        true,
      );
    } catch {
      // no-op fallback
    }

    target.outputData = {
      ...(target.outputData || {}),
      falsePositive: true,
      falsePositiveAt: new Date().toISOString(),
    };

    if (selectedRecord.value?.id === recordId) {
      selectedRecord.value = { ...target };
    }
  }

  function setDateRange(startDate: string, endDate: string) {
    filters.startDate = startDate;
    filters.endDate = endDate;
  }

  return {
    records,
    selectedRecord,
    filters,
    isLoading,
    error,
    stats,
    filteredRecords,
    loadRecords,
    selectRecord,
    exportReport,
    markFalsePositive,
    setDateRange,
  };
});
