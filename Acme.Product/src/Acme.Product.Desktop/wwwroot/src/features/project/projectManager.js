/**
 * 工程管理模块
 * 负责工程的创建、打开、保存、列表管理
 */

import httpClient from '../../core/messaging/httpClient.js';
import { createSignal } from '../../core/state/store.js';

// 工程状态
const [getCurrentProject, setCurrentProject, subscribeProject] = createSignal(null);
const [getProjectList, setProjectList, subscribeProjectList] = createSignal([]);
const [getRecentProjects, setRecentProjects, subscribeRecentProjects] = createSignal([]);

class ProjectManager {
    constructor() {
        this.currentProject = null;
        this.unsavedChanges = false;
    }

    /**
     * 获取工程列表
     */
    async getProjectList() {
        try {
            const projects = await httpClient.get('/projects');
            setProjectList(projects);
            return projects;
        } catch (error) {
            console.error('[ProjectManager] 获取工程列表失败:', error);
            throw error;
        }
    }

    /**
     * 获取最近打开的工程
     */
    async getRecentProjects(count = 10) {
        try {
            const projects = await httpClient.get(`/projects/recent?count=${count}`);
            setRecentProjects(projects);
            return projects;
        } catch (error) {
            console.error('[ProjectManager] 获取最近工程失败:', error);
            throw error;
        }
    }

    /**
     * 搜索工程
     */
    async searchProjects(keyword) {
        try {
            const projects = await httpClient.get(`/projects/search?keyword=${encodeURIComponent(keyword)}`);
            return projects;
        } catch (error) {
            console.error('[ProjectManager] 搜索工程失败:', error);
            throw error;
        }
    }

    /**
     * 创建新工程
     */
    async createProject(name, description = '') {
        try {
            const project = await httpClient.post('/projects', {
                name,
                description
            });
            
            this.currentProject = project;
            setCurrentProject(project);
            this.unsavedChanges = false;
            
            console.log('[ProjectManager] 工程创建成功:', project.id);
            return project;
        } catch (error) {
            console.error('[ProjectManager] 创建工程失败:', error);
            throw error;
        }
    }

    async createDemoProject(mode = 'full') {
        const endpoint = mode === 'simple' ? '/demo/create-simple' : '/demo/create';

        try {
            const project = await httpClient.post(endpoint);

            this.currentProject = project;
            setCurrentProject(project);
            this.unsavedChanges = false;
            this.updateStatusBar(project);

            console.log('[ProjectManager] 示例工程创建成功:', project.id, '| mode:', mode);
            return project;
        } catch (error) {
            console.error('[ProjectManager] 创建示例工程失败:', error);
            throw error;
        }
    }

    async getDemoGuide() {
        try {
            return await httpClient.get('/demo/guide');
        } catch (error) {
            console.error('[ProjectManager] 获取示例工程引导失败:', error);
            throw error;
        }
    }

    /**
     * 打开工程
     */
    async openProject(projectId) {
        try {
            const project = await httpClient.get(`/projects/${projectId}`);
            
            this.currentProject = project;
            setCurrentProject(project);
            this.unsavedChanges = false;
            
            // 更新状态栏
            this.updateStatusBar(project);
            
            console.log('[ProjectManager] 工程打开成功:', project.id);
            return project;
        } catch (error) {
            console.error('[ProjectManager] 打开工程失败:', error);
            throw error;
        }
    }

    /**
     * 保存工程
     */
    async saveProject(projectData = null) {
        if (!this.currentProject) {
            throw new Error('没有打开的工程');
        }

        const data = projectData || this.currentProject;
        
        try {
            // 更新工程基本信息
            await httpClient.put(`/projects/${this.currentProject.id}`, {
                name: data.name,
                description: data.description
            });

            // 保存流程
            if (data.flow) {
                await httpClient.put(`/projects/${this.currentProject.id}/flow`, data.flow);
            }

            this.unsavedChanges = false;
            this.updateTitle();
            
            console.log('[ProjectManager] 工程保存成功:', this.currentProject.id);
            return true;
        } catch (error) {
            console.error('[ProjectManager] 保存工程失败:', error);
            throw error;
        }
    }

    /**
     * 删除工程
     */
    async deleteProject(projectId) {
        try {
            await httpClient.delete(`/projects/${projectId}`);
            
            // 如果删除的是当前工程，清空当前工程
            if (this.currentProject && this.currentProject.id === projectId) {
                this.closeProject();
            }
            
            console.log('[ProjectManager] 工程删除成功:', projectId);
            return true;
        } catch (error) {
            console.error('[ProjectManager] 删除工程失败:', error);
            throw error;
        }
    }

    /**
     * 关闭当前工程
     */
    closeProject() {
        if (this.unsavedChanges) {
            const confirm = window.confirm('工程有未保存的更改，是否保存？');
            if (confirm) {
                this.saveProject();
            }
        }

        this.currentProject = null;
        setCurrentProject(null);
        this.unsavedChanges = false;
        this.updateStatusBar(null);
    }

    /**
     * 更新当前工程数据
     */
    updateProject(updates) {
        if (!this.currentProject) return;

        this.currentProject = {
            ...this.currentProject,
            ...updates,
            modifiedAt: new Date().toISOString()
        };

        setCurrentProject(this.currentProject);
        this.unsavedChanges = true;
        this.updateTitle();
    }

    /**
     * 更新流程
     */
    updateFlow(flowData) {
        if (!this.currentProject) return;

        this.currentProject.flow = flowData;
        this.unsavedChanges = true;
    }

    /**
     * 检查是否有未保存的更改
     */
    hasUnsavedChanges() {
        return this.unsavedChanges;
    }

    /**
     * 获取当前工程
     */
    getCurrentProject() {
        return this.currentProject;
    }

    /**
     * 更新状态栏
     */
    updateStatusBar(project) {
        const projectNameEl = document.getElementById('project-name');
        const versionEl = document.getElementById('version');
        
        if (projectNameEl) {
            projectNameEl.textContent = project ? project.name : '未命名工程';
        }
        
        if (versionEl && project) {
            versionEl.textContent = `v${project.version || '1.0.0'}`;
        }
    }

    /**
     * 更新窗口标题
     */
    updateTitle() {
        const unsavedMark = this.unsavedChanges ? ' *' : '';
        const projectName = this.currentProject ? this.currentProject.name : '未命名';
        document.title = `${projectName}${unsavedMark} - ClearVision`;
    }

    /**
     * 导出工程
     */
    async exportProject(projectId, format = 'json') {
        try {
            const project = await httpClient.get(`/projects/${projectId}`);
            
            let content, filename, mimeType;
            
            switch (format) {
                case 'json':
                    content = JSON.stringify(project, null, 2);
                    filename = `${project.name}.json`;
                    mimeType = 'application/json';
                    break;
                default:
                    throw new Error(`不支持的导出格式: ${format}`);
            }

            // 下载文件
            const blob = new Blob([content], { type: mimeType });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
            
            return true;
        } catch (error) {
            console.error('[ProjectManager] 导出工程失败:', error);
            throw error;
        }
    }

    /**
     * 导入工程
     */
    async importProject(file) {
        try {
            const content = await file.text();
            const projectData = JSON.parse(content);
            
            // 创建新工程
            const project = await this.createProject(
                projectData.name || '导入的工程',
                projectData.description || ''
            );

            // 导入流程数据
            if (projectData.flow) {
                await this.updateFlow(projectData.flow);
                await this.saveProject();
            }

            return project;
        } catch (error) {
            console.error('[ProjectManager] 导入工程失败:', error);
            throw error;
        }
    }
}

// 创建单例
const projectManager = new ProjectManager();

export default projectManager;
export { 
    projectManager, 
    getCurrentProject, 
    setCurrentProject,
    subscribeProject,
    getProjectList,
    getRecentProjects
};
