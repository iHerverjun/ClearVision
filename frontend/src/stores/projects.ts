import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { apiClient } from '../services/api';
import { webMessageBridge } from '../services/bridge';
import { BridgeMessageType } from '../services/bridge.types';
import { ENDPOINTS } from '../services/endpoints';
import type { LegacyProjectConfig } from '../services/flowSerializer';
import { useFlowStore } from './flow';

export interface Project {
  id: string;
  name: string;
  description?: string;
  type: string;
  updatedAt: string;
  isActive?: boolean;
}

const toIsoString = (value: unknown) => {
  if (typeof value === 'string' && value.trim()) {
    return value;
  }
  return new Date().toISOString();
};

const normalizeProject = (raw: any): Project => {
  const typeRaw = String(raw?.type || raw?.projectType || raw?.category || 'General');
  const type = typeRaw.trim().toLowerCase() === 'general' ? '通用' : typeRaw;
  const updatedAt = toIsoString(raw?.updatedAt || raw?.modifiedAt || raw?.lastOpenedAt || raw?.createdAt);
  return {
    id: String(raw?.id || raw?.projectId || ''),
    name: String(raw?.name || '未命名工程'),
    description: typeof raw?.description === 'string' ? raw.description : '',
    type,
    updatedAt,
    isActive: Boolean(raw?.isActive),
  };
};

const toLegacyProject = (projectDetail: any): LegacyProjectConfig | null => {
  // Legacy payload directly from backend or file
  if (projectDetail?.Nodes && projectDetail?.Edges) {
    return projectDetail as LegacyProjectConfig;
  }

  const flow = projectDetail?.flow;
  if (!flow || !Array.isArray(flow.operators)) {
    return null;
  }

  const nodes = flow.operators.map((operator: any) => ({
    Id: String(operator.id),
    Name: String(operator.name || operator.id),
    Type: String(operator.type || 'Unknown'),
    Location: {
      X: Number(operator.x || 0),
      Y: Number(operator.y || 0),
    },
    InputPorts: (operator.inputPorts || []).map((port: any) => ({
      Id: String(port.id || ''),
      Name: String(port.name || ''),
      Description: '',
      DataType: String(port.dataType || 'Unknown'),
    })),
    OutputPorts: (operator.outputPorts || []).map((port: any) => ({
      Id: String(port.id || ''),
      Name: String(port.name || ''),
      Description: '',
      DataType: String(port.dataType || 'Unknown'),
    })),
    Configuration: Object.fromEntries(
      (operator.parameters || []).map((parameter: any) => [
        String(parameter.name || ''),
        parameter.value ?? parameter.defaultValue ?? null,
      ]),
    ),
  }));

  const edges = (flow.connections || []).map((connection: any) => ({
    Id: String(connection.id || `${connection.sourceOperatorId}-${connection.targetOperatorId}`),
    SourceOperatorId: String(connection.sourceOperatorId || ''),
    SourcePortId: String(connection.sourcePortId || ''),
    TargetOperatorId: String(connection.targetOperatorId || ''),
    TargetPortId: String(connection.targetPortId || ''),
  }));

  return {
    Version: String(projectDetail?.version || '1.0.0'),
    Nodes: nodes,
    Edges: edges,
  };
};

export const useProjectsStore = defineStore('projects', () => {
  const flowStore = useFlowStore();

  const projects = ref<Project[]>([]);
  const currentProject = ref<Project | null>(null);
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  const projectCount = computed(() => projects.value.length);
  const recentProjects = computed(() =>
    [...projects.value]
      .sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime())
      .slice(0, 5),
  );

  async function loadProjects() {
    isLoading.value = true;
    error.value = null;

    try {
      const bridgeResponse = await webMessageBridge.sendMessage(
        BridgeMessageType.ProjectListQuery,
        {},
        true,
      );
      if (Array.isArray(bridgeResponse?.projects)) {
        projects.value = bridgeResponse.projects.map(normalizeProject);
      } else {
        const response = await apiClient.get(ENDPOINTS.Project.List);
        projects.value = (Array.isArray(response) ? response : []).map(normalizeProject);
      }
    } catch (bridgeError) {
      try {
        const response = await apiClient.get(ENDPOINTS.Project.List);
        projects.value = (Array.isArray(response) ? response : []).map(normalizeProject);
      } catch (apiError: any) {
        error.value = apiError?.message || '加载工程列表失败';
      }
    } finally {
      projects.value.sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime());
      isLoading.value = false;
    }
  }

  async function createProject(name: string, type = 'General', description = '') {
    isLoading.value = true;
    error.value = null;

    try {
      const bridgeResponse = await webMessageBridge.sendMessage(
        BridgeMessageType.ProjectCreateCommand,
        { name, type, description },
        true,
      );
      const projectRaw = bridgeResponse?.project || {
        id: bridgeResponse?.projectId,
        name,
        description,
        type,
      };
      const created = normalizeProject(projectRaw);
      projects.value = [created, ...projects.value.filter((project) => project.id !== created.id)];
      return created;
    } catch {
      const response = await apiClient.post(ENDPOINTS.Project.Create, { name, description });
      const created = normalizeProject(response);
      created.type = type;
      projects.value = [created, ...projects.value.filter((project) => project.id !== created.id)];
      return created;
    } finally {
      isLoading.value = false;
    }
  }

  async function deleteProject(id: string) {
    isLoading.value = true;
    error.value = null;

    try {
      await webMessageBridge.sendMessage(
        BridgeMessageType.ProjectDeleteCommand,
        { projectId: id },
        true,
      );
    } catch {
      await apiClient.delete(ENDPOINTS.Project.Delete(id));
    } finally {
      projects.value = projects.value.filter((project) => project.id !== id);
      if (currentProject.value?.id === id) {
        currentProject.value = null;
      }
      isLoading.value = false;
    }
  }

  async function openProject(id: string) {
    isLoading.value = true;
    error.value = null;

    try {
      let projectDetail: any = null;

      try {
        const bridgeResponse = await webMessageBridge.sendMessage(
          BridgeMessageType.ProjectOpenCommand,
          { projectId: id },
          true,
        );
        projectDetail = bridgeResponse?.project || null;
      } catch {
        // fallback to REST below
      }

      if (!projectDetail) {
        try {
          projectDetail = await apiClient.get(ENDPOINTS.Project.Get(id));
        } catch {
          projectDetail = null;
        }
      }

      const selected = projectDetail ? normalizeProject(projectDetail) : projects.value.find((item) => item.id === id) || null;
      if (!selected) {
        throw new Error('未找到工程');
      }

      currentProject.value = selected;

      const legacyProject = projectDetail ? toLegacyProject(projectDetail) : null;
      if (legacyProject) {
        flowStore.loadLegacyProject(legacyProject);
      }

      projects.value = projects.value.map((project) => ({
        ...project,
        isActive: project.id === id,
      }));

      return selected;
    } catch (openError: any) {
      error.value = openError?.message || '打开工程失败';
      throw openError;
    } finally {
      isLoading.value = false;
    }
  }

  function selectProject(project: Project | null) {
    currentProject.value = project;
    projects.value = projects.value.map((item) => ({
      ...item,
      isActive: project ? item.id === project.id : false,
    }));
  }

  return {
    projects,
    currentProject,
    isLoading,
    error,
    projectCount,
    recentProjects,
    loadProjects,
    createProject,
    deleteProject,
    openProject,
    selectProject,
  };
});
