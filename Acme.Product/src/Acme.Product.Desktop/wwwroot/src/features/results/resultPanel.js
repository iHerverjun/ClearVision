/**
 * 结果面板组件 - 阶段二增强版
 * 现代化数据可视化仪表板
 */

import httpClient from '../../core/messaging/httpClient.js';
import { renderDiagnosticsCardsHtml } from '../inspection/analysisCardsPanel.js';

class ResultPanel {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        this.results = [];
        this.filteredResults = [];
        this.projectId = null;
        this.serverReport = null;
        this.serverAnalysis = null;
        this.serverAnalysisSource = 'local';
        this._analyticsRefreshTimer = null;
        this._historyRefreshTimer = null;
        this.statistics = {
            total: 0,
            ok: 0,
            ng: 0,
            error: 0,
            avgTime: 0
        };
        
        // 分页
        this.currentPage = 1;
        this.pageSize = 12;
        this.totalPages = 1;
        this.totalResultCount = 0;
        this.serverPageIndex = 0;
        this.serverPaged = false;
        this.historyLoader = null;
        
        // 筛选
        this.filters = {
            status: 'all',
            defectType: 'all',
            startTime: null,
            endTime: null
        };
        
        // 时间范围
        this.timeRange = 'today';
        
        // 趋势图数据
        this.trendData = [];
        
        // 缺陷类型统计
        this.defectTypes = {};
        
        // 绑定事件
        this.bindEvents();
        
        console.log('[ResultPanel] 结果面板初始化完成');
    }
    
    /**
     * 绑定事件
     */
    bindEvents() {
        // 时间范围选择
        document.querySelectorAll('.time-range-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                document.querySelectorAll('.time-range-btn').forEach(b => b.classList.remove('active'));
                e.target.classList.add('active');
                this.setTimeRange(e.target.dataset.range);
            });
        });
        
        // 状态筛选
        const statusFilter = document.getElementById('filter-status');
        if (statusFilter) {
            statusFilter.addEventListener('change', (e) => {
                this.setFilter('status', e.target.value);
            });
        }
        
        // 缺陷类型筛选
        const defectTypeFilter = document.getElementById('filter-defect-type');
        if (defectTypeFilter) {
            defectTypeFilter.addEventListener('change', (e) => {
                this.setFilter('defectType', e.target.value);
            });
        }
        
        // 导出下拉菜单
        const exportDropdown = document.getElementById('export-dropdown');
        const exportBtn = document.getElementById('btn-export-results');
        if (exportBtn && exportDropdown) {
            exportBtn.addEventListener('click', () => {
                exportDropdown.classList.toggle('open');
            });
            
            // 导出选项
            exportDropdown.querySelectorAll('.export-menu-item').forEach(item => {
                item.addEventListener('click', () => {
                    const format = item.dataset.format;
                    this.exportResults(format);
                    exportDropdown.classList.remove('open');
                });
            });
            
            // 点击外部关闭
            document.addEventListener('click', (e) => {
                if (!exportDropdown.contains(e.target)) {
                    exportDropdown.classList.remove('open');
                }
            });
        }
    }
    
    /**
     * 设置时间范围
     */
    setTimeRange(range) {
        this.timeRange = range;
        const { startTime, endTime } = this.getTimeRangeBounds(range);
        this.filters.startTime = startTime;
        this.filters.endTime = endTime;
        this.currentPage = 1;
        
        this.applyFilters();
        this.render();

        if (this.projectId && this.historyLoader) {
            this.requestHistoryPage(0).catch(error => {
                console.warn('[ResultPanel] 刷新服务端历史失败:', error);
            });
        }

        if (this.projectId) {
            this.loadServerAnalytics().catch(error => {
                console.warn('[ResultPanel] 刷新服务端分析失败:', error);
            });
        }
    }
    getTimeRangeBounds(range = this.timeRange) {
        const now = new Date();

        switch (range) {
            case 'today':
                return {
                    startTime: new Date(now.getFullYear(), now.getMonth(), now.getDate()),
                    endTime: now
                };
            case 'week': {
                const weekStart = new Date(now);
                weekStart.setDate(now.getDate() - now.getDay());
                weekStart.setHours(0, 0, 0, 0);
                return {
                    startTime: weekStart,
                    endTime: now
                };
            }
            case 'month':
                return {
                    startTime: new Date(now.getFullYear(), now.getMonth(), 1),
                    endTime: now
                };
            case 'custom':
                return {
                    startTime: this.filters.startTime,
                    endTime: this.filters.endTime
                };
            default:
                return {
                    startTime: null,
                    endTime: null
                };
        }
    }

    setProjectContext(projectId) {
        const normalizedProjectId = projectId || null;
        if (this.projectId !== normalizedProjectId) {
            this.projectId = normalizedProjectId;
            this.serverReport = null;
            this.serverAnalysis = null;
            this.serverAnalysisSource = 'local';
            this.totalResultCount = 0;
            this.serverPageIndex = 0;
            this.serverPaged = false;
        }
    }

    setHistoryLoader(loader) {
        this.historyLoader = typeof loader === 'function' ? loader : null;
    }

    hasLocalPageFilters() {
        return !this.serverPaged && (this.filters.status !== 'all' || this.filters.defectType !== 'all');
    }

    isServerPaginationActive() {
        return this.serverPaged && !this.hasLocalPageFilters();
    }

    isClientFilteringServerPage() {
        return this.serverPaged && this.hasLocalPageFilters();
    }

    getVisiblePageResults() {
        return this.serverPaged
            ? this.filteredResults
            : this.filteredResults.slice(
                (this.currentPage - 1) * this.pageSize,
                Math.min(this.currentPage * this.pageSize, this.filteredResults.length)
            );
    }

    getResultsScopeSummary(pageResults = this.getVisiblePageResults()) {
        if (this.isClientFilteringServerPage()) {
            return `当前仅筛选已加载页：本页命中 ${this.filteredResults.length} 条，未覆盖其余 ${Math.max(this.totalResultCount - pageResults.length, 0)} 条历史记录`;
        }

        if (this.serverPaged) {
            return `当前页 ${pageResults.length} 条 / 共 ${this.totalResultCount} 条记录`;
        }

        return `共 ${this.filteredResults.length} 条记录`;
    }

    requestHistoryPage(pageIndex = 0) {
        if (!this.historyLoader || !this.projectId) {
            return Promise.resolve(false);
        }

        return this.historyLoader({
            pageIndex,
            pageSize: this.pageSize,
            ...this.getAnalyticsQueryParams()
        });
    }
    getAnalyticsQueryParams() {
        const { startTime, endTime } = this.getTimeRangeBounds(this.timeRange);
        const params = {};

        if (startTime instanceof Date && !Number.isNaN(startTime.getTime())) {
            params.startTime = startTime.toISOString();
        }

        if (endTime instanceof Date && !Number.isNaN(endTime.getTime())) {
            params.endTime = endTime.toISOString();
        }

        if (this.filters.status && this.filters.status !== 'all') {
            params.status = this.filters.status;
        }

        if (this.filters.defectType && this.filters.defectType !== 'all') {
            params.defectType = this.filters.defectType;
        }

        return params;
    }

    queueServerAnalyticsRefresh(delayMs = 800) {
        if (!this.projectId) {
            return;
        }

        if (this._analyticsRefreshTimer) {
            clearTimeout(this._analyticsRefreshTimer);
        }

        this._analyticsRefreshTimer = window.setTimeout(() => {
            this._analyticsRefreshTimer = null;
            this.loadServerAnalytics().catch(error => {
                console.warn('[ResultPanel] Server analytics refresh failed:', error);
            });
        }, delayMs);
    }

    queueServerHistoryRefresh(delayMs = 400) {
        if (!this.projectId || !this.historyLoader) {
            return;
        }

        if (this._historyRefreshTimer) {
            clearTimeout(this._historyRefreshTimer);
        }

        this._historyRefreshTimer = window.setTimeout(() => {
            this._historyRefreshTimer = null;
            this.requestHistoryPage(0).catch(error => {
                console.warn('[ResultPanel] Server history refresh failed:', error);
            });
        }, delayMs);
    }

    normalizeStatistics(statistics) {
        if (!statistics || typeof statistics !== 'object') {
            return null;
        }

        return {
            total: statistics.totalCount ?? statistics.TotalCount ?? 0,
            ok: statistics.okCount ?? statistics.OKCount ?? 0,
            ng: statistics.ngCount ?? statistics.NGCount ?? 0,
            error: statistics.errorCount ?? statistics.ErrorCount ?? 0,
            avgTime: Math.round(statistics.averageProcessingTimeMs ?? statistics.AverageProcessingTimeMs ?? 0)
        };
    }

    normalizeDefectDistribution(defectDistribution) {
        const items = defectDistribution?.items || defectDistribution?.Items || [];
        return items.reduce((accumulator, item) => {
            const defectType = item.defectType || item.DefectType || '未知';
            const count = item.count ?? item.Count ?? 0;
            accumulator[defectType] = count;
            return accumulator;
        }, {});
    }

    normalizeTrendPoints(trend) {
        const points = trend?.dataPoints || trend?.DataPoints || [];
        return points.map(point => ({
            time: new Date(point.timestamp || point.Timestamp || Date.now()),
            status: (point.ngCount ?? point.NGCount ?? 0) > 0
                ? 'NG'
                : ((point.errorCount ?? point.ErrorCount ?? 0) > 0 ? 'Error' : 'OK'),
            defectCount: point.defectCount ?? point.DefectCount ?? 0
        }));
    }

    applyServerAnalysis({ report = null, statistics = null, defectDistribution = null, trend = null } = {}) {
        const normalizedStatistics = this.normalizeStatistics(
            report?.summary || report?.Summary || statistics
        );
        const normalizedDefects = this.normalizeDefectDistribution(
            report?.defectDistribution || report?.DefectDistribution || defectDistribution
        );
        const normalizedTrend = this.normalizeTrendPoints(
            report?.hourlyTrend || report?.HourlyTrend || trend
        );

        if (normalizedStatistics) {
            this.statistics = normalizedStatistics;
        }

        if (Object.keys(normalizedDefects).length > 0) {
            this.defectTypes = normalizedDefects;
            this.updateDefectTypeFilter();
        }

        if (normalizedTrend.length > 0) {
            this.trendData = normalizedTrend;
        }

        this.serverReport = report || this.serverReport;
        this.serverAnalysis = {
            statistics: normalizedStatistics,
            defectTypes: normalizedDefects,
            trendData: normalizedTrend
        };
        this.serverAnalysisSource = 'server';
    }

    async loadServerAnalytics(projectId = this.projectId) {
        if (!projectId) {
            return;
        }

        const commonParams = this.getAnalyticsQueryParams();

        const reportPromise = httpClient.get(`/analysis/report/${projectId}`, commonParams)
            .catch(error => {
                console.warn('[ResultPanel] Failed to load analysis report:', error);
                return null;
            });

        const statisticsPromise = httpClient.get(`/analysis/statistics/${projectId}`, commonParams)
            .catch(error => {
                console.warn('[ResultPanel] Failed to load statistics:', error);
                return null;
            });
        const defectDistributionPromise = httpClient.get(`/analysis/defect-distribution/${projectId}`, commonParams)
            .catch(error => {
                console.warn('[ResultPanel] 获取缺陷分布失败:', error);
                return null;
            });

        const trendPromise = commonParams.startTime && commonParams.endTime
            ? httpClient.get(`/analysis/trend/${projectId}`, {
                interval: this.timeRange === 'today' ? 'Hour' : 'Day',
                startTime: commonParams.startTime,
                endTime: commonParams.endTime
            }).catch(error => {
                console.warn('[ResultPanel] 获取趋势分析失败:', error);
                return null;
            })
            : Promise.resolve(null);

        const [report, statistics, defectDistribution, trend] = await Promise.all([
            reportPromise,
            statisticsPromise,
            defectDistributionPromise,
            trendPromise
        ]);

        if (report || statistics || defectDistribution || trend) {
            this.applyServerAnalysis({ report, statistics, defectDistribution, trend });
            this.render();
            return;
        }

        if (this.serverPaged) {
            this.serverReport = null;
            this.serverAnalysis = null;
            this.serverAnalysisSource = 'server-unavailable';
            this.statistics = {
                total: 0,
                ok: 0,
                ng: 0,
                error: 0,
                avgTime: 0
            };
            this.defectTypes = {};
            this.trendData = [];
            this.updateDefectTypeFilter();
            this.render();
            return;
        }

        this.serverAnalysisSource = 'local';

        if (statistics) {
            this.statistics = {
                total: statistics.totalCount ?? statistics.TotalCount ?? this.statistics.total,
                ok: statistics.okCount ?? statistics.OKCount ?? this.statistics.ok,
                ng: statistics.ngCount ?? statistics.NGCount ?? this.statistics.ng,
                error: statistics.errorCount ?? statistics.ErrorCount ?? this.statistics.error,
                avgTime: Math.round(statistics.averageProcessingTimeMs ?? statistics.AverageProcessingTimeMs ?? this.statistics.avgTime ?? 0)
            };
        }

        if (defectDistribution?.items || defectDistribution?.Items) {
            const items = defectDistribution.items || defectDistribution.Items || [];
            this.defectTypes = items.reduce((accumulator, item) => {
                const defectType = item.defectType || item.DefectType || '未知';
                const count = item.count ?? item.Count ?? 0;
                accumulator[defectType] = count;
                return accumulator;
            }, {});
        }

        if (trend?.dataPoints || trend?.DataPoints) {
            const points = trend.dataPoints || trend.DataPoints || [];
            this.trendData = points.map(point => ({
                time: new Date(point.timestamp || point.Timestamp || Date.now()),
                status: (point.ngCount ?? point.NGCount ?? 0) > 0
                    ? 'NG'
                    : ((point.errorCount ?? point.ErrorCount ?? 0) > 0 ? 'Error' : 'OK'),
                defectCount: point.defectCount ?? point.DefectCount ?? 0
            }));
        }

        this.render();
    }
    
    /**
     * 更新统计
     */
    updateStatistics(stats) {
        this.statistics = { ...this.statistics, ...stats };
        this.renderKPIs();
        this.renderYieldChart();
    }
    
    /**
     * 添加结果
     */
    addResult(result) {
        if (this.serverPaged) {
            if (this.projectId) {
                this.queueServerHistoryRefresh();
                this.queueServerAnalyticsRefresh();
            }
            return;
        }

        this.results.unshift(result);
        this.applyFilters();
        
        // 更新统计
        this.statistics.total++;
        if (result.status === 'OK') {
            this.statistics.ok++;
        } else if (result.status === 'NG') {
            this.statistics.ng++;
        } else if (result.status === 'Error') {
            this.statistics.error++;
        }
        
        // 更新平均耗时
        if (result.processingTime) {
            const validResults = this.results.filter(r => r.processingTime);
            const totalTime = validResults.reduce((sum, r) => sum + r.processingTime, 0);
            this.statistics.avgTime = validResults.length > 0 ? Math.round(totalTime / validResults.length) : 0;
        }
        
        // 更新趋势图数据
        this.trendData.push({
            time: new Date(result.timestamp || Date.now()),
            status: result.status,
            defectCount: result.defects?.length || 0
        });
        if (this.trendData.length > 100) {
            this.trendData.shift();
        }
        
        // 更新缺陷类型统计
        if (result.defects) {
            result.defects.forEach(defect => {
                const type = defect.type || defect.description || '未知';
                this.defectTypes[type] = (this.defectTypes[type] || 0) + 1;
            });
        }

        if (this.projectId) {
            this.queueServerAnalyticsRefresh();
        }

        this.render();
    }
    
    /**
     * 加载历史结果
     */
    loadResults(results, { totalCount = null, pageIndex = 0, pageSize = this.pageSize, serverPaged = false } = {}) {
        this.results = Array.isArray(results) ? results : [];
        this.serverPaged = !!serverPaged;
        this.serverPageIndex = Math.max(0, pageIndex);
        this.pageSize = Number.isFinite(pageSize) && pageSize > 0 ? pageSize : this.pageSize;
        this.totalResultCount = Number.isFinite(totalCount) ? totalCount : this.results.length;
        this.currentPage = this.serverPaged ? this.serverPageIndex + 1 : 1;
        this.applyFilters();

        if (!this.serverPaged) {
            this.calculateStatistics();
            this.updateTrendData();
        } else if (this.serverAnalysisSource === 'server') {
            this.updateDefectTypeFilter();
        }

        this.render();
    }
    /**
     * 计算统计
     */
    calculateStatistics() {
        const total = this.results.length;
        const ok = this.results.filter(r => r.status === 'OK').length;
        const ng = this.results.filter(r => r.status === 'NG').length;
        const error = this.results.filter(r => r.status === 'Error').length;
        
        const validResults = this.results.filter(r => r.processingTime);
        const totalTime = validResults.reduce((sum, r) => sum + (r.processingTime || 0), 0);
        const avgTime = validResults.length > 0 ? Math.round(totalTime / validResults.length) : 0;
        
        this.statistics = { total, ok, ng, error, avgTime };
        
        // 重新计算缺陷类型
        this.defectTypes = {};
        this.results.forEach(r => {
            if (r.defects) {
                r.defects.forEach(defect => {
                    const type = defect.type || defect.description || '未知';
                    this.defectTypes[type] = (this.defectTypes[type] || 0) + 1;
                });
            }
        });
        
        // 更新缺陷类型下拉框
        this.updateDefectTypeFilter();
    }
    
    /**
     * 更新缺陷类型筛选器
     */
    updateDefectTypeFilter() {
        const select = document.getElementById('filter-defect-type');
        if (!select) return;
        
        const currentValue = select.value;
        select.innerHTML = '<option value="all">全部</option>';
        
        Object.keys(this.defectTypes).forEach(type => {
            const option = document.createElement('option');
            option.value = type;
            option.textContent = `${type} (${this.defectTypes[type]})`;
            select.appendChild(option);
        });
        
        select.value = currentValue;
    }
    
    /**
     * 更新趋势图数据
     */
    updateTrendData() {
        this.trendData = this.results
            .slice(0, 100)
            .map(r => ({
                time: new Date(r.timestamp || Date.now()),
                status: r.status,
                defectCount: r.defects?.length || 0
            }))
            .reverse();
    }
    
    applyFilters() {
        if (this.serverPaged) {
            this.filteredResults = [...this.results];
            this.totalPages = Math.ceil(this.totalResultCount / this.pageSize) || 1;
            this.currentPage = this.serverPageIndex + 1;
            return;
        }

        this.filteredResults = this.results.filter(r => {
            // 状态筛选
            if (this.filters.status !== 'all' && r.status?.toLowerCase() !== this.filters.status) {
                return false;
            }
            
            // 缺陷类型筛选
            if (this.filters.defectType !== 'all') {
                const hasDefectType = r.defects?.some(d => 
                    (d.type || d.description || '未知') === this.filters.defectType
                );
                if (!hasDefectType) return false;
            }
            
            // 时间范围筛选
            if (this.filters.startTime) {
                const resultTime = new Date(r.timestamp).getTime();
                if (resultTime < this.filters.startTime.getTime()) {
                    return false;
                }
            }
            
            if (this.filters.endTime) {
                const resultTime = new Date(r.timestamp).getTime();
                if (resultTime > this.filters.endTime.getTime()) {
                    return false;
                }
            }
            
            return true;
        });

        this.totalPages = Math.ceil(this.filteredResults.length / this.pageSize) || 1;
        
        if (this.currentPage > this.totalPages) {
            this.currentPage = this.totalPages;
        }
    }
    
    /**
     * 设置筛选条件
     */
    setFilter(type, value) {
        this.filters[type] = value;
        this.currentPage = 1;
        if (this.serverPaged && this.projectId) {
            this.requestHistoryPage(0).catch(error => {
                console.warn('[ResultPanel] 刷新服务端历史失败:', error);
            });
            this.loadServerAnalytics().catch(error => {
                console.warn('[ResultPanel] 刷新服务端分析失败:', error);
            });
            return;
        }

        this.applyFilters();
        this.render();
    }
    
    /**
     * 翻页
     */
    goToPage(page) {
        if (page < 1 || page > this.totalPages) return;

        if (this.isServerPaginationActive()) {
            this.requestHistoryPage(page - 1).catch(error => {
                console.warn('[ResultPanel] 翻页加载服务端历史失败:', error);
            });
            return;
        }

        this.currentPage = page;
        this.render();
    }
    
    /**
     * 清空结果
     */
    clear() {
        this.results = [];
        this.filteredResults = [];
        this.trendData = [];
        this.defectTypes = {};
        this.statistics = { total: 0, ok: 0, ng: 0, error: 0, avgTime: 0 };
        this.serverReport = null;
        this.serverAnalysis = null;
        this.serverAnalysisSource = 'local';
        if (this._analyticsRefreshTimer) {
            clearTimeout(this._analyticsRefreshTimer);
            this._analyticsRefreshTimer = null;
        }
        if (this._historyRefreshTimer) {
            clearTimeout(this._historyRefreshTimer);
            this._historyRefreshTimer = null;
        }
        this.totalResultCount = 0;
        this.serverPageIndex = 0;
        this.serverPaged = false;
        this.currentPage = 1;
        this.applyFilters();
        this.render();
    }

    getResultImageSrc(result) {
        if (!result) {
            return '';
        }

        if (result.imageUrl) {
            return result.imageUrl;
        }

        if (result.imageId) {
            return httpClient.buildRequestUrl(`/images/${result.imageId}`);
        }

        if (result.imageData) {
            return `data:image/png;base64,${result.imageData}`;
        }

        return '';
    }
    
    /**
     * 渲染面板
     */
    render() {
        this.renderKPIs();
        this.renderYieldChart();
        this.renderDefectDistribution();
        this.renderTrendChart();
        this.renderResultsList();
        this.renderPagination();
    }
    
    /**
     * 渲染KPI卡片
     */
    renderKPIs() {
        const { total, ok, ng, error, avgTime } = this.statistics;
        const yieldRate = total > 0 ? ((ok / total) * 100).toFixed(1) : '0';
        
        const kpiTotal = document.getElementById('kpi-total');
        const kpiOk = document.getElementById('kpi-ok');
        const kpiNg = document.getElementById('kpi-ng');
        const kpiYield = document.getElementById('kpi-yield');
        const kpiAvgTime = document.getElementById('kpi-avg-time');
        
        if (kpiTotal) kpiTotal.textContent = total;
        if (kpiOk) kpiOk.textContent = ok;
        if (kpiNg) kpiNg.textContent = ng;
        if (kpiYield) kpiYield.textContent = `${yieldRate}%`;
        if (kpiAvgTime) kpiAvgTime.textContent = `${avgTime}ms`;
    }
    
    /**
     * 渲染良率环形图
     */
    renderYieldChart() {
        const { total, ok } = this.statistics;
        const yieldRate = total > 0 ? (ok / total) : 0;
        const percentage = (yieldRate * 100).toFixed(1);
        
        // 更新百分比文字
        const yieldPercentage = document.getElementById('yield-percentage');
        if (yieldPercentage) yieldPercentage.textContent = `${percentage}%`;
        
        // 更新SVG环形图
        const fillCircle = document.getElementById('yield-chart-fill');
        if (fillCircle) {
            const circumference = 2 * Math.PI * 60; // r=60
            const offset = circumference * (1 - yieldRate);
            fillCircle.style.strokeDasharray = circumference;
            fillCircle.style.strokeDashoffset = offset;
            fillCircle.style.stroke = yieldRate > 0.9 ? '#2ecc71' : yieldRate > 0.7 ? '#f1c40f' : '#e74c3c';
            fillCircle.style.transition = 'stroke-dashoffset 0.5s ease';
        }
    }
    
    /**
     * 渲染缺陷类型分布
     */
    renderDefectDistribution() {
        const container = document.getElementById('defect-bars');
        if (!container) return;
        
        const types = Object.entries(this.defectTypes);
        if (types.length === 0) {
            container.innerHTML = '<p class="empty-text">暂无缺陷数据</p>';
            return;
        }
        
        const maxCount = Math.max(...types.map(([, count]) => count));
        
        container.innerHTML = types.map(([type, count]) => `
            <div class="defect-bar-item">
                <div class="defect-bar-header">
                    <span class="defect-bar-label">${type}</span>
                    <span class="defect-bar-value">${count}</span>
                </div>
                <div class="defect-bar-track">
                    <div class="defect-bar-fill" style="width:${(count/maxCount*100).toFixed(1)}%"></div>
                </div>
            </div>
        `).join('');
    }
    
    /**
     * 渲染趋势图
     */
    renderTrendChart() {
        const canvas = document.getElementById('trend-canvas');
        if (!canvas) return;
        
        // Optimize for Retina display
        const dpr = window.devicePixelRatio || 1;
        const rect = canvas.parentElement.getBoundingClientRect();
        
        canvas.width = rect.width * dpr;
        canvas.height = rect.height * dpr;
        
        const ctx = canvas.getContext('2d');
        ctx.scale(dpr, dpr);
        
        const width = rect.width;
        const height = rect.height;
        const padding = 20;
        
        // 清空画布
        ctx.clearRect(0, 0, width, height);
        
        if (this.trendData.length < 2) {
            ctx.fillStyle = '#64748b';
            ctx.font = '14px "Inter", sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText('数据不足，无法显示趋势图', width / 2, height / 2);
            return;
        }
        
        // 绘制背景网格
        ctx.strokeStyle = 'rgba(255,255,255,0.05)';
        ctx.lineWidth = 1;
        for (let i = 0; i <= 5; i++) {
            const y = padding + (height - 2 * padding) * i / 5;
            ctx.beginPath();
            ctx.moveTo(padding, y);
            ctx.lineTo(width - padding, y);
            ctx.stroke();
        }
        
        const chartWidth = width - 2 * padding;
        const chartHeight = height - 2 * padding;
        const stepX = chartWidth / (this.trendData.length - 1);
        
        // 状态映射到Y坐标
        const statusY = {
            'OK': padding + chartHeight * 0.2,
            'NG': padding + chartHeight * 0.5,
            'Error': padding + chartHeight * 0.8
        };
        
        // 绘制连线
        ctx.strokeStyle = 'rgba(59, 130, 246, 0.6)';
        ctx.lineWidth = 2;
        ctx.beginPath();
        
        this.trendData.forEach((point, index) => {
            const x = padding + index * stepX;
            const y = statusY[point.status] || padding + chartHeight * 0.5;
            
            if (index === 0) {
                ctx.moveTo(x, y);
            } else {
                ctx.lineTo(x, y);
            }
        });
        ctx.stroke();
        
        // 绘制数据点
        this.trendData.forEach((point, index) => {
            const x = padding + index * stepX;
            const y = statusY[point.status] || padding + chartHeight * 0.5;
            
            if (point.status === 'OK') {
                ctx.fillStyle = '#10b981';
            } else if (point.status === 'NG') {
                ctx.fillStyle = '#ef4444';
            } else {
                ctx.fillStyle = '#f59e0b';
            }
            
            ctx.beginPath();
            ctx.arc(x, y, 4, 0, Math.PI * 2);
            ctx.fill();
        });
        
        // 绘制Y轴标签
        ctx.fillStyle = '#94a3b8';
        ctx.font = '12px sans-serif';
        ctx.textAlign = 'right';
        ctx.fillText('OK', padding - 8, padding + chartHeight * 0.2 + 4);
        ctx.fillText('NG', padding - 8, padding + chartHeight * 0.5 + 4);
        ctx.fillText('Error', padding - 8, padding + chartHeight * 0.8 + 4);
    }
    
    /**
     * 渲染结果列表
     */
    renderResultsList() {
        const gridContainer = document.getElementById('results-grid');
        const countInfo = document.getElementById('results-count-info');
        if (!gridContainer) return;

        const pageResults = this.getVisiblePageResults();

        if (countInfo) {
            countInfo.textContent = this.getResultsScopeSummary(pageResults);
        }

        if (pageResults.length === 0) {
            const emptyText = this.isClientFilteringServerPage()
                ? '当前页未命中筛选条件。当前筛选只作用于已加载页，可调整时间范围后重新翻页加载。'
                : '暂无检测结果';
            gridContainer.innerHTML = `<p class="empty-text">${emptyText}</p>`;
            return;
        }

        gridContainer.innerHTML = pageResults.map((result, index) => {
            const statusClass = result.status?.toLowerCase() || 'unknown';
            const time = result.timestamp ? new Date(result.timestamp).toLocaleTimeString() : '--:--:--';
            const processingTime = result.processingTime || result.executionTimeMs || '--';
            const outputDataHtml = this.renderAnalysisDataPreview(result.analysisData);

            return `
                <div class="result-card result-${statusClass}" data-index="${index}" style="cursor:pointer;">
                    <div class="result-card-header">
                        <span class="result-status-badge ${statusClass}">${result.status || 'Unknown'}</span>
                        <span class="result-time">${time}</span>
                    </div>
                    <div class="result-card-body">
                        <span class="result-processing-time">${processingTime}ms</span>
                        ${result.defects?.length > 0 ? `<span class="result-defect-count">${result.defects.length} 缺陷</span>` : ''}
                        ${outputDataHtml}
                    </div>
                </div>
            `;
        }).join('');

        gridContainer.querySelectorAll('.result-card').forEach(card => {
            card.addEventListener('click', (e) => {
                const index = parseInt(e.currentTarget.dataset.index, 10);
                const result = pageResults[index];
                if (result) {
                    this.showResultDetail(result);
                }
            });
        });
    }
    
    /**
     * 渲染分页控件
     */
    renderPagination() {
        const paginationContainer = document.getElementById('results-pagination');
        if (!paginationContainer) return;

        if (this.isClientFilteringServerPage()) {
            paginationContainer.innerHTML = `
                <div class="empty-text" style="margin:0; text-align:center;">
                    当前筛选仅作用于已加载页，分页已暂停以避免误认为正在筛选全量历史。
                </div>
            `;
            return;
        }
        
        if (this.totalPages <= 1) {
            paginationContainer.innerHTML = '';
            return;
        }
        
        let pageButtons = '';
        const maxVisiblePages = 5;
        let startPage = Math.max(1, this.currentPage - Math.floor(maxVisiblePages / 2));
        let endPage = Math.min(this.totalPages, startPage + maxVisiblePages - 1);
        
        if (endPage - startPage < maxVisiblePages - 1) {
            startPage = Math.max(1, endPage - maxVisiblePages + 1);
        }
        
        // 上一页
        pageButtons += `<button class="page-btn ${this.currentPage === 1 ? 'disabled' : ''}" 
            ${this.currentPage === 1 ? 'disabled' : ''} data-page="${this.currentPage - 1}">«</button>`;
        
        if (startPage > 1) {
            pageButtons += `<button class="page-btn" data-page="1">1</button>`;
            if (startPage > 2) pageButtons += `<span class="page-ellipsis">...</span>`;
        }
        
        for (let i = startPage; i <= endPage; i++) {
            pageButtons += `<button class="page-btn ${i === this.currentPage ? 'active' : ''}" data-page="${i}">${i}</button>`;
        }
        
        if (endPage < this.totalPages) {
            if (endPage < this.totalPages - 1) pageButtons += `<span class="page-ellipsis">...</span>`;
            pageButtons += `<button class="page-btn" data-page="${this.totalPages}">${this.totalPages}</button>`;
        }
        
        // 下一页
        pageButtons += `<button class="page-btn ${this.currentPage === this.totalPages ? 'disabled' : ''}" 
            ${this.currentPage === this.totalPages ? 'disabled' : ''} data-page="${this.currentPage + 1}">»</button>`;
        
        paginationContainer.innerHTML = pageButtons;
        
        // 绑定分页事件
        paginationContainer.querySelectorAll('.page-btn:not(.disabled)').forEach(btn => {
            btn.addEventListener('click', () => {
                const page = parseInt(btn.dataset.page);
                if (page) this.goToPage(page);
            });
        });
    }
    
    /**
     * 导出结果
     */
    exportResults(format = 'json') {
        if (this.isClientFilteringServerPage()) {
            window.alert('当前导出仅包含已加载页中的筛选结果，并非全量历史记录。');
        }

        const exportContext = this.getExportPayload(format);
        if (exportContext) {
            const blob = new Blob([exportContext.content], { type: exportContext.mimeType });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = exportContext.filename;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
            return;
        }

        if (this.projectId && this.serverPaged) {
            window.alert('服务端报告尚未就绪，当前结果页不再回退导出本地页数据。请稍后重试或先确认服务端分析链路正常。');
            return;
        }

        if (this.filteredResults.length === 0) {
            alert('没有可导出的结果');
            return;
        }
        
        let content, filename, mimeType;
        const filenamePrefix = this.isClientFilteringServerPage()
            ? 'inspection_results_current_page'
            : 'inspection_results';
        
        switch (format) {
            case 'json':
                content = JSON.stringify(this.filteredResults, null, 2);
                filename = `${filenamePrefix}_${Date.now()}.json`;
                mimeType = 'application/json';
                break;
            case 'csv':
            case 'excel':
                content = this.convertToCSV(this.filteredResults);
                filename = `${filenamePrefix}_${Date.now()}.csv`;
                mimeType = 'text/csv';
                break;
            default:
                throw new Error(`不支持的导出格式: ${format}`);
        }
        
        const blob = new Blob([content], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
    
    /**
     * 转换为 CSV
     */
    getExportPayload(format = 'json') {
        const timestamp = Date.now();
        const report = this.serverReport;

        if (report && this.projectId) {
            if (format === 'json') {
                return {
                    content: JSON.stringify(report, null, 2),
                    filename: `inspection_report_${timestamp}.json`,
                    mimeType: 'application/json'
                };
            }

            if (format === 'csv' || format === 'excel') {
                return {
                    content: this.convertReportToCSV(report),
                    filename: `inspection_report_${timestamp}.csv`,
                    mimeType: 'text/csv'
                };
            }
        }

        return null;
    }

    convertToCSV(results) {
        const headers = ['时间', '状态', '缺陷数', '处理时间(ms)', '置信度'];
        const rows = results.map(r => [
            r.timestamp ? new Date(r.timestamp).toISOString() : '',
            r.status,
            r.defects?.length || 0,
            r.processingTime || r.executionTimeMs || '',
            r.defects?.[0]?.confidenceScore ? (r.defects[0].confidenceScore * 100).toFixed(1) + '%' : ''
        ]);
        
        return [headers.join(','), ...rows.map(row => row.join(','))].join('\n');
    }
    
    /**
     * 显示结果详情
     */
    convertReportToCSV(report) {
        const summary = report?.summary || report?.Summary || {};
        const period = report?.period || report?.Period || {};
        const recommendations = report?.recommendations || report?.Recommendations || [];
        const defectItems = report?.defectDistribution?.items
            || report?.defectDistribution?.Items
            || report?.DefectDistribution?.Items
            || [];
        const trendItems = report?.hourlyTrend?.dataPoints
            || report?.hourlyTrend?.DataPoints
            || report?.HourlyTrend?.DataPoints
            || [];

        const lines = [
            'Section,Key,Value',
            `Summary,ProjectId,${report?.projectId || report?.ProjectId || this.projectId || ''}`,
            `Summary,GeneratedAt,${report?.generatedAt || report?.GeneratedAt || ''}`,
            `Summary,StartTime,${period.startTime || period.StartTime || ''}`,
            `Summary,EndTime,${period.endTime || period.EndTime || ''}`,
            `Summary,TotalCount,${summary.totalCount ?? summary.TotalCount ?? 0}`,
            `Summary,OKCount,${summary.okCount ?? summary.OKCount ?? 0}`,
            `Summary,NGCount,${summary.ngCount ?? summary.NGCount ?? 0}`,
            `Summary,ErrorCount,${summary.errorCount ?? summary.ErrorCount ?? 0}`,
            `Summary,AverageProcessingTimeMs,${summary.averageProcessingTimeMs ?? summary.AverageProcessingTimeMs ?? 0}`
        ];

        defectItems.forEach(item => {
            lines.push(`DefectDistribution,${item.defectType || item.DefectType || '未知'},${item.count ?? item.Count ?? 0}`);
        });

        trendItems.forEach(point => {
            lines.push(`Trend,${point.timestamp || point.Timestamp || ''},${point.totalCount ?? point.TotalCount ?? 0}`);
        });

        recommendations.forEach((recommendation, index) => {
            lines.push(`Recommendation,${index + 1},"${String(recommendation).replace(/"/g, '""')}"`);
        });

        return lines.join('\n');
    }

    showResultDetail(result) {
        console.log('[ResultPanel] 查看结果详情:', result);
        
        const modal = document.createElement('div');
        modal.className = 'result-detail-modal';
        
        const statusClass = result.status?.toLowerCase() || 'unknown';
        const time = result.timestamp ? new Date(result.timestamp).toLocaleString() : '--';
        const processingTime = result.processingTime || result.executionTimeMs || '--';
        const imageSrc = this.getResultImageSrc(result);
        
        modal.innerHTML = `
            <div class="result-detail-overlay"></div>
            <div class="result-detail-content">
                <div class="result-detail-header">
                    <h3>检测结果详情</h3>
                    <span class="result-status-badge ${statusClass}" style="font-size:12px;padding:4px 12px;">${result.status || 'Unknown'}</span>
                    <button class="result-detail-close">✕</button>
                </div>
                <div class="result-detail-body">
                    ${imageSrc ? `<div class="result-detail-image"><img src="${imageSrc}" alt="检测结果图像" /></div>` : ''}
                    <div class="result-detail-data">
                        <div class="detail-section">
                            <div class="detail-item"><span class="detail-label">状态</span><span class="detail-value status-${statusClass}">${result.status || '--'}</span></div>
                            <div class="detail-item"><span class="detail-label">时间</span><span class="detail-value">${time}</span></div>
                            <div class="detail-item"><span class="detail-label">处理耗时</span><span class="detail-value">${processingTime}ms</span></div>
                        </div>
                        ${this.renderAnalysisDataSection(result.analysisData)}
                        ${this.renderDiagnosticsSection(result.outputData, result.status)}
                        ${this.renderOutputDataTable(result.outputData)}
                        ${result.defects?.length > 0 ? `
                            <div class="detail-section">
                                <div class="detail-section-title">缺陷列表 (${result.defects.length})</div>
                                ${result.defects.map(d => `
                                    <div class="detail-item">
                                        <span class="detail-label">${d.type || d.description || '未知'}</span>
                                        <span class="detail-value">${d.confidenceScore ? (d.confidenceScore * 100).toFixed(1) + '%' : '--'}</span>
                                    </div>
                                `).join('')}
                            </div>
                        ` : ''}
                    </div>
                </div>
            </div>
        `;
        
        document.body.appendChild(modal);
        // 入场动画
        requestAnimationFrame(() => modal.classList.add('visible'));
        
        const closeModal = () => {
            modal.classList.remove('visible');
            setTimeout(() => modal.remove(), 200);
        };
        modal.querySelector('.result-detail-close').addEventListener('click', closeModal);
        modal.querySelector('.result-detail-overlay').addEventListener('click', closeModal);
    }
    
    renderAnalysisDataPreview(analysisData) {
        const cards = Array.isArray(analysisData?.cards) ? analysisData.cards : [];
        if (cards.length === 0) {
            return '';
        }

        const items = cards.slice(0, 3).map(card => {
            const summary = this.getAnalysisCardSummary(card);
            return `<div class="output-data-item output-text">
                <span class="output-label">${this.escapeHtml(card.title || card.category || '分析卡片')}</span>
                <span class="output-value" title="${this.escapeHtml(summary)}">${this.escapeHtml(summary.length > 30 ? summary.substring(0, 30) + '...' : summary)}</span>
            </div>`;
        });

        return items.length > 0 ? `<div class="output-data-preview">${items.join('')}</div>` : '';
    }
    
    /**
     * 渲染输出数据表格（详情弹窗内完整展示）
     */
    renderOutputDataTable(outputData) {
        if (!outputData || typeof outputData !== 'object' || Object.keys(outputData).length === 0) return '';
        
        const rows = [];
        let hiddenCount = 0;
        for (const [key, value] of Object.entries(outputData)) {
            if (this.shouldHideOutputDetailEntry(key, value, outputData)) {
                hiddenCount += 1;
                continue;
            }
            
            let displayValue = '';
            let typeClass = '';
            
            if (typeof value === 'string') {
                displayValue = this.escapeHtml(value);
                typeClass = 'type-string';
            } else if (typeof value === 'number') {
                displayValue = Number.isInteger(value) ? String(value) : value.toFixed(4);
                typeClass = 'type-number';
            } else if (typeof value === 'boolean') {
                displayValue = value ? '✓ True' : '✗ False';
                typeClass = value ? 'type-bool-true' : 'type-bool-false';
            } else if (value === null || value === undefined) {
                displayValue = '--';
                typeClass = 'type-null';
            } else {
                displayValue = this.escapeHtml(JSON.stringify(value).substring(0, 100));
                typeClass = 'type-object';
            }
            
            rows.push(`<div class="detail-item ${typeClass}"><span class="detail-label">${this.escapeHtml(key)}</span><span class="detail-value">${displayValue}</span></div>`);
        }
        
        if (rows.length === 0 && hiddenCount === 0) return '';

        const hiddenNotice = hiddenCount > 0
            ? `<div class="detail-item type-null"><span class="detail-label">说明</span><span class="detail-value">已隐藏 ${hiddenCount} 个导出/技术字段</span></div>`
            : '';

        return `<div class="detail-section"><div class="detail-section-title">原始输出数据（调试）</div>${rows.join('')}${hiddenNotice}</div>`;
    }

    renderAnalysisDataSection(analysisData) {
        const cards = Array.isArray(analysisData?.cards) ? analysisData.cards : [];
        if (cards.length === 0) {
            return '';
        }

        const sections = cards.map(card => {
            const fields = Array.isArray(card?.fields) ? card.fields : [];
            const rows = fields.map(field => `<div class="detail-item">
                <span class="detail-label">${this.escapeHtml(field.label || field.key || '--')}</span>
                <span class="detail-value">${this.escapeHtml(this.formatAnalysisFieldValue(field.value))}${field.unit ? ` ${this.escapeHtml(field.unit)}` : ''}</span>
            </div>`).join('');

            return `
                <div class="detail-section">
                    <div class="detail-section-title">${this.escapeHtml(card.title || card.category || '分析卡片')}</div>
                    ${rows || '<div class="detail-item"><span class="detail-label">内容</span><span class="detail-value">--</span></div>'}
                </div>
            `;
        }).join('');

        return sections;
    }

    renderDiagnosticsSection(outputData, fallbackStatus) {
        if (!outputData || typeof outputData !== 'object') {
            return '';
        }

        const diagnosticsHtml = renderDiagnosticsCardsHtml(outputData, fallbackStatus || 'OK', {
            containerClass: 'analysis-cards-container ac-diagnostics-inline ac-diagnostics-detail'
        });

        if (!diagnosticsHtml) {
            return '';
        }

        return `
            <div class="detail-section">
                <div class="detail-section-title">诊断面板</div>
                ${diagnosticsHtml}
            </div>
        `;
    }

    getAnalysisCardSummary(card) {
        const fields = Array.isArray(card?.fields) ? card.fields : [];
        const firstField = fields.find(field => field && field.value !== undefined && field.value !== null);
        if (!firstField) {
            return card?.status || '--';
        }

        const label = firstField.label || firstField.key || '值';
        const value = this.formatAnalysisFieldValue(firstField.value);
        return `${label}: ${value}`;
    }

    formatAnalysisFieldValue(value) {
        if (typeof value === 'number') {
            return Number.isInteger(value) ? String(value) : value.toFixed(3);
        }

        if (typeof value === 'boolean') {
            return value ? 'True' : 'False';
        }

        if (value === null || value === undefined) {
            return '--';
        }

        if (typeof value === 'object') {
            return JSON.stringify(value);
        }

        return String(value);
    }

    isMeaningfulRecognitionText(value, outputData, sourceKey = '') {
        if (typeof value !== 'string') {
            return false;
        }

        const text = value.trim();
        if (!text || text.length >= 200) {
            return false;
        }

        return !this.isStructuredExportText(text, outputData, sourceKey);
    }

    isStructuredExportText(value, outputData, sourceKey = '') {
        const text = String(value || '').trim();
        if (!text) {
            return false;
        }

        if (this.isExportMetadataKey(sourceKey)) {
            return true;
        }

        const looksLikeStructuredPayload =
            (text.startsWith('{') && text.endsWith('}')) ||
            (text.startsWith('[') && text.endsWith(']'));
        if (!looksLikeStructuredPayload) {
            return false;
        }

        const exportHintKeys = ['Format', 'format', 'SaveToFile', 'saveToFile', 'Output', 'output', 'FilePath', 'filePath', 'SaveError', 'saveError'];
        const hasExportHints = Object.keys(outputData || {}).some(key => exportHintKeys.includes(key));
        if (hasExportHints) {
            return true;
        }

        return text.includes('"Format"')
            || text.includes('"SaveToFile"')
            || text.includes('"FilePath"')
            || text.includes('"SaveError"');
    }

    isExportMetadataKey(key) {
        return [
            'format',
            'savetofile',
            'output',
            'filepath',
            'saveerror',
            'success'
        ].includes(String(key || '').toLowerCase());
    }

    isTechnicalCollectionKey(key) {
        return [
            'detectionlist',
            'objects',
            'defects',
            'rawcandidatecount',
            'visualizationdetectioncount',
            'internalnmsenabled',
            'visualizationdetections'
        ].includes(String(key || '').toLowerCase());
    }

    shouldHideOutputDetailEntry(key, value, outputData) {
        const normalizedKey = String(key || '').toLowerCase();
        if (normalizedKey === 'image') {
            return true;
        }

        if (this.isExportMetadataKey(normalizedKey)) {
            return true;
        }

        if (this.isTechnicalCollectionKey(normalizedKey)) {
            return true;
        }

        if (typeof value === 'string') {
            if (value.length > 500) {
                return true;
            }

            if (this.isStructuredExportText(value, outputData, key)) {
                return true;
            }
        }

        return false;
    }
    
    /**
     * HTML转义
     */
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = String(text);
        return div.innerHTML;
    }
    
    /**
     * 获取最新结果
     */
    getLatestResult() {
        return this.filteredResults[0] || null;
    }
    
    /**
     * 获取所有结果
     */
    getAllResults() {
        return [...this.filteredResults];
    }
}

// 创建全局实例供HTML事件使用
let resultPanel = null;

export default ResultPanel;
export { ResultPanel };

