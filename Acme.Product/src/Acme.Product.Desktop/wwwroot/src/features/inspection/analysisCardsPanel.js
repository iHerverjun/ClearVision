/**
 * 分析卡片面板组件
 * 优先渲染显式 `analysisData` 契约；在线序诊断场景下，也支持渲染结构化 `Diagnostics` 输出。
 * 作者：蘅芜君
 */

// 专业 SVG 图标表
const ICONS = {
    distance: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path><polyline points="3.27 6.96 12 12.01 20.73 6.96"></polyline><line x1="12" y1="22.08" x2="12" y2="12"></line></svg>`,
    angle: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 20h18"></path><path d="M3 20L19.4 3"></path><path d="M10 20a7 7 0 0 1 5-5.6"></path></svg>`,
    radius: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="12" x2="12" y2="2"></line></svg>`,
    area: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect><line x1="3" y1="9" x2="21" y2="9"></line><line x1="9" y1="21" x2="9" y2="9"></line></svg>`,
    ruler: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.3 15.3l-7.6-7.6a2 2 0 0 0-2.8 0l-2.8 2.8a2 2 0 0 0 0 2.8l7.6 7.6a2 2 0 0 0 2.8 0l2.8-2.8a2 2 0 0 0 0-2.8z"></path><path d="M14.5 12.5L16 11"></path><path d="M11.5 15.5L13 14"></path><path d="M8.5 18.5L10 17"></path><path d="M17.5 9.5L19 8"></path></svg>`,
    ocr: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="4 7 4 4 20 4 20 7"></polyline><line x1="9" y1="20" x2="15" y2="20"></line><line x1="12" y1="4" x2="12" y2="20"></line></svg>`,
    barcode: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 5v14"></path><path d="M8 5v14"></path><path d="M12 5v14"></path><path d="M17 5v14"></path><path d="M21 5v14"></path></svg>`,
    defect: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>`,
    target: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="8"></circle><circle cx="12" cy="12" r="3"></circle><line x1="12" y1="2" x2="12" y2="5"></line><line x1="12" y1="19" x2="12" y2="22"></line><line x1="2" y1="12" x2="5" y2="12"></line><line x1="19" y1="12" x2="22" y2="12"></line></svg>`,
    sequence: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7h8"></path><path d="M4 12h12"></path><path d="M4 17h6"></path><path d="M16 6l4 3-4 3"></path><path d="M16 15l4 3-4 3"></path></svg>`,
    filter: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="22 3 2 3 10 12.5 10 19 14 21 14 12.5 22 3"></polygon></svg>`,
    note: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 4h16v16H4z"></path><path d="M8 8h8"></path><path d="M8 12h8"></path><path d="M8 16h5"></path></svg>`,
    match: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 3h18v18H3z"></path><path d="M12 8v8"></path><path d="M8 12h8"></path></svg>`,
    generic: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="8" y1="6" x2="21" y2="6"></line><line x1="8" y1="12" x2="21" y2="12"></line><line x1="8" y1="18" x2="21" y2="18"></line><line x1="3" y1="6" x2="3.01" y2="6"></line><line x1="3" y1="12" x2="3.01" y2="12"></line><line x1="3" y1="18" x2="3.01" y2="18"></line></svg>`,
    check: `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>`,
    cross: `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>`
};

const MEASUREMENT_ICON_MAP = {
    distance: ICONS.distance,
    radius: ICONS.radius,
    diameter: ICONS.radius,
    angle: ICONS.angle,
    length: ICONS.ruler,
    area: ICONS.area,
    perimeter: ICONS.area,
    width: ICONS.ruler,
    height: ICONS.ruler
};

function escapeHtml(text) {
    if (text === null || text === undefined) {
        return '';
    }

    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function readFirstDefined(...values) {
    return values.find(value => value !== undefined && value !== null);
}

function isPlainObject(value) {
    return !!value && typeof value === 'object' && !Array.isArray(value);
}

function toNumberOrNull(value) {
    if (typeof value === 'number' && Number.isFinite(value)) {
        return value;
    }

    const parsed = Number.parseFloat(String(value ?? '').trim());
    return Number.isFinite(parsed) ? parsed : null;
}

function normalizeStringList(value) {
    if (Array.isArray(value)) {
        return value
            .map(item => String(item ?? '').trim())
            .filter(Boolean);
    }

    if (typeof value === 'string') {
        const trimmed = value.trim();
        if (!trimmed) {
            return [];
        }

        if (trimmed.includes(',')) {
            return trimmed
                .split(',')
                .map(item => item.trim())
                .filter(Boolean);
        }

        return [trimmed];
    }

    return [];
}

function looksLikeSequenceDiagnostics(value) {
    if (!isPlainObject(value)) {
        return false;
    }

    return [
        'ActualOrder',
        'actualOrder',
        'ExpectedLabels',
        'expectedLabels',
        'IsMatch',
        'isMatch',
        'MissingLabels',
        'missingLabels',
        'DuplicateLabels',
        'duplicateLabels'
    ].some(key => key in value);
}

function looksLikeNmsDiagnostics(value) {
    if (!isPlainObject(value)) {
        return false;
    }

    return [
        'InputCount',
        'inputCount',
        'CandidateCount',
        'candidateCount',
        'KeptCount',
        'keptCount',
        'SuppressedCount',
        'suppressedCount'
    ].some(key => key in value);
}

function findDiagnosticsCandidate(outputData, predicate) {
    if (!isPlainObject(outputData)) {
        return null;
    }

    const candidates = [
        outputData.Diagnostics,
        outputData.diagnostics,
        outputData.Result,
        outputData.result,
        outputData.Data,
        outputData.data,
        outputData
    ];

    return candidates.find(predicate) || null;
}

function normalizeSequenceDiagnostics(outputData) {
    const candidate = findDiagnosticsCandidate(outputData, looksLikeSequenceDiagnostics);
    if (!candidate) {
        return null;
    }

    return {
        isMatch: Boolean(readFirstDefined(candidate.IsMatch, candidate.isMatch)),
        expectedLabels: normalizeStringList(readFirstDefined(candidate.ExpectedLabels, candidate.expectedLabels)),
        actualOrder: normalizeStringList(readFirstDefined(candidate.ActualOrder, candidate.actualOrder)),
        missingLabels: normalizeStringList(readFirstDefined(candidate.MissingLabels, candidate.missingLabels)),
        duplicateLabels: normalizeStringList(readFirstDefined(candidate.DuplicateLabels, candidate.duplicateLabels)),
        receivedCount: toNumberOrNull(readFirstDefined(candidate.ReceivedCount, candidate.receivedCount)),
        filteredCount: toNumberOrNull(readFirstDefined(candidate.FilteredCount, candidate.filteredCount)),
        sortedCount: toNumberOrNull(readFirstDefined(candidate.SortedCount, candidate.sortedCount, candidate.Count, candidate.count)),
        expectedCount: toNumberOrNull(readFirstDefined(candidate.ExpectedCount, candidate.expectedCount)),
        minConfidence: toNumberOrNull(readFirstDefined(candidate.MinConfidence, candidate.minConfidence, candidate.RequiredMinConfidence, candidate.requiredMinConfidence)),
        sortBy: readFirstDefined(candidate.SortBy, candidate.sortBy, 'CenterX'),
        direction: readFirstDefined(candidate.Direction, candidate.direction, 'Ascending'),
        message: String(readFirstDefined(candidate.Message, candidate.message, outputData.Text, outputData.text) || '').trim()
    };
}

function normalizeNmsDiagnostics(outputData) {
    const candidate = findDiagnosticsCandidate(outputData, looksLikeNmsDiagnostics);
    if (!candidate) {
        return null;
    }

    return {
        inputCount: toNumberOrNull(readFirstDefined(candidate.InputCount, candidate.inputCount)),
        candidateCount: toNumberOrNull(readFirstDefined(candidate.CandidateCount, candidate.candidateCount)),
        keptCount: toNumberOrNull(readFirstDefined(candidate.KeptCount, candidate.keptCount, candidate.Count, candidate.count)),
        suppressedCount: toNumberOrNull(readFirstDefined(candidate.SuppressedCount, candidate.suppressedCount)),
        scoreThreshold: toNumberOrNull(readFirstDefined(candidate.ScoreThreshold, candidate.scoreThreshold)),
        iouThreshold: toNumberOrNull(readFirstDefined(candidate.IouThreshold, candidate.iouThreshold)),
        maxDetections: toNumberOrNull(readFirstDefined(candidate.MaxDetections, candidate.maxDetections))
    };
}

function tryGetDetectionCount(value) {
    if (typeof value === 'number' && Number.isFinite(value)) {
        return value;
    }

    if (Array.isArray(value)) {
        return value.length;
    }

    if (isPlainObject(value)) {
        if (typeof value.Count === 'number') {
            return value.Count;
        }
        if (typeof value.count === 'number') {
            return value.count;
        }
        if (Array.isArray(value.Detections)) {
            return value.Detections.length;
        }
        if (Array.isArray(value.detections)) {
            return value.detections.length;
        }
    }

    return null;
}

function normalizeDeepLearningPreviewDiagnostics(outputData) {
    if (!isPlainObject(outputData)) {
        return null;
    }

    const internalNmsEnabled = readFirstDefined(outputData.InternalNmsEnabled, outputData.internalNmsEnabled);
    if (internalNmsEnabled !== false) {
        return null;
    }

    const detectionMode = String(readFirstDefined(outputData.DetectionMode, outputData.detectionMode, 'Object'));
    const rawCandidates = toNumberOrNull(readFirstDefined(
        outputData.RawCandidateCount,
        outputData.rawCandidateCount,
        detectionMode.toLowerCase() === 'object'
            ? readFirstDefined(outputData.ObjectCount, outputData.objectCount)
            : readFirstDefined(outputData.DefectCount, outputData.defectCount)
    ));
    const visualizedCount = toNumberOrNull(readFirstDefined(
        outputData.VisualizationDetectionCount,
        outputData.visualizationDetectionCount
    ));

    return {
        detectionMode,
        internalNmsEnabled: false,
        rawCandidateCount: rawCandidates,
        visualizedCount,
        visualOwner: 'Preview-NMS',
        businessOwner: 'BoxNms'
    };
}

function formatDiagnosticValue(field) {
    const { value, variant } = field || {};
    if ((variant === 'sequence' || variant === 'labels') && Array.isArray(value)) {
        if (value.length === 0) {
            return '<span class="ac-diagnostic-empty">无</span>';
        }

        const className = variant === 'sequence' ? 'ac-diagnostic-sequence' : 'ac-diagnostic-chip-list';
        const items = value.map(item => `<span class="ac-diagnostic-chip">${escapeHtml(String(item))}</span>`).join('');
        return `<div class="${className}">${items}</div>`;
    }

    if (variant === 'status') {
        const text = String(value ?? '').trim();
        const lowerText = text.toLowerCase();
        const isNegativeStatus = text.includes('不匹配')
            || text.includes('未匹配')
            || lowerText.includes('mismatch')
            || lowerText.includes('not match')
            || lowerText.includes('no match');
        const isOk = !isNegativeStatus
            && (text.includes('匹配') || /\bmatch(?:ed)?\b/.test(lowerText));
        return `<span class="ac-diagnostic-status ${isOk ? 'ok' : 'ng'}">${escapeHtml(String(value))}</span>`;
    }

    if (typeof value === 'number') {
        return `<span class="ac-diagnostic-number">${Number.isInteger(value) ? value : value.toFixed(3)}</span>`;
    }

    if (value === null || value === undefined || value === '') {
        return '<span class="ac-diagnostic-empty">--</span>';
    }

    return `<span class="ac-diagnostic-text">${escapeHtml(String(value))}</span>`;
}

function renderDiagnosticCardHtml(card, fallbackStatus, options = {}) {
    const compact = Boolean(options.compact);
    const status = String(card?.status || fallbackStatus || 'OK').toUpperCase();
    const statusClass = status === 'NG' || status === 'ERROR' ? 'ng' : 'ok';
    const icon = card?.icon || ICONS.note;
    const fields = Array.isArray(card?.fields)
        ? (compact ? card.fields.slice(0, 4) : card.fields)
        : [];
    const rows = fields.map(field => `
        <div class="ac-diagnostic-row">
            <span class="ac-diagnostic-label">${escapeHtml(field.label || '--')}</span>
            <div class="ac-diagnostic-value">${formatDiagnosticValue(field)}</div>
        </div>
    `).join('');
    const message = card?.message
        ? `<div class="ac-diagnostic-message ${statusClass}">${escapeHtml(card.message)}</div>`
        : '';
    const hint = compact && Array.isArray(card?.fields) && card.fields.length > fields.length
        ? `<div class="ac-diagnostic-hint">还有 ${card.fields.length - fields.length} 项诊断字段，请在结果详情查看完整信息。</div>`
        : '';

    return `
        <div class="ac-card ac-card-diagnostic ac-status-${statusClass}">
            <div class="ac-card-header">
                <span class="ac-card-icon">${icon}</span>
                <span class="ac-card-title">${escapeHtml(card?.title || '诊断卡片')}</span>
                <span class="ac-diagnostic-badge ${statusClass}">${status === 'OK' ? 'OK' : 'NG'}</span>
            </div>
            <div class="ac-card-body">
                ${message}
                <div class="ac-diagnostic-grid">${rows || '<div class="ac-diagnostic-empty">暂无诊断字段</div>'}</div>
                ${hint}
            </div>
        </div>
    `;
}

export function buildDiagnosticsAnalysisData(outputData, fallbackStatus = 'OK') {
    if (!isPlainObject(outputData)) {
        return null;
    }

    const cards = [];
    const sequence = normalizeSequenceDiagnostics(outputData);
    const nms = normalizeNmsDiagnostics(outputData);
    const deepLearningPreview = normalizeDeepLearningPreviewDiagnostics(outputData);

    if (deepLearningPreview) {
        cards.push({
            category: 'diagnostic',
            type: 'preview-nms',
            title: '预览去重说明',
            status: 'OK',
            icon: ICONS.note,
            message: '当前预览图使用 Preview-NMS 收敛重叠框，仅用于可视化；真正业务去重仍由下游 BoxNms 执行。',
            priority: 130,
            fields: [
                { label: '检测模式', value: deepLearningPreview.detectionMode },
                { label: '原始候选框', value: deepLearningPreview.rawCandidateCount },
                { label: '预览图显示框', value: deepLearningPreview.visualizedCount },
                { label: '预览去重层', value: deepLearningPreview.visualOwner },
                { label: '业务去重层', value: deepLearningPreview.businessOwner },
                { label: '内部 NMS', value: 'Off' }
            ]
        });
    }

    if (sequence) {
        cards.push({
            category: 'diagnostic',
            type: 'sequence',
            title: '线序诊断',
            status: sequence.isMatch ? 'OK' : 'NG',
            icon: ICONS.sequence,
            message: sequence.message,
            priority: sequence.isMatch ? 110 : 160,
            fields: [
                { label: '判定结果', value: sequence.isMatch ? '匹配' : '不匹配', variant: 'status' },
                { label: '预期顺序', value: sequence.expectedLabels, variant: 'sequence' },
                { label: '实际顺序', value: sequence.actualOrder, variant: 'sequence' },
                { label: '缺失标签', value: sequence.missingLabels, variant: 'labels' },
                { label: '重复标签', value: sequence.duplicateLabels, variant: 'labels' },
                { label: '接收数量', value: sequence.receivedCount },
                { label: '过滤后数量', value: sequence.filteredCount },
                { label: '最终排序数量', value: sequence.sortedCount },
                { label: '预期数量', value: sequence.expectedCount },
                { label: '最小置信度', value: sequence.minConfidence },
                { label: '排序字段', value: sequence.sortBy },
                { label: '排序方向', value: sequence.direction }
            ]
        });
    }

    if (nms) {
        const nmsStatus = (nms.inputCount ?? 0) > 0 && (nms.keptCount ?? 0) === 0 ? 'NG' : 'OK';
        const nmsMessage = nmsStatus === 'NG'
            ? '候选框在分数阈值或 NMS 阶段被全部过滤，请优先检查 BoxNms.ScoreThreshold 和 IoU 阈值。'
            : '';
        cards.push({
            category: 'diagnostic',
            type: 'nms',
            title: '候选框诊断',
            status: nmsStatus,
            icon: ICONS.filter,
            message: nmsMessage,
            priority: 120,
            fields: [
                { label: '输入框数', value: nms.inputCount },
                { label: '候选框数', value: nms.candidateCount },
                { label: '保留框数', value: nms.keptCount },
                { label: '抑制框数', value: nms.suppressedCount },
                { label: '分数阈值', value: nms.scoreThreshold },
                { label: 'IoU 阈值', value: nms.iouThreshold },
                { label: '最大保留数', value: nms.maxDetections }
            ]
        });
    }

    if (cards.length === 0) {
        return null;
    }

    return {
        version: 1,
        cards
    };
}

export function renderDiagnosticsCardsHtml(outputData, fallbackStatus = 'OK', options = {}) {
    const analysisData = buildDiagnosticsAnalysisData(outputData, fallbackStatus);
    if (!analysisData || !Array.isArray(analysisData.cards) || analysisData.cards.length === 0) {
        return options.emptyState || '';
    }

    const containerClass = options.containerClass || 'analysis-cards-container ac-diagnostics-inline';
    return `
        <div class="${containerClass}">
            ${analysisData.cards.map(card => renderDiagnosticCardHtml(card, fallbackStatus, options)).join('')}
        </div>
    `;
}

class AnalysisCardsPanel {
    constructor(containerId) {
        this.container = document.getElementById(containerId);

        if (!this.container) {
            console.warn('[AnalysisCardsPanel] 找不到容器:', containerId);
        }

        console.log('[AnalysisCardsPanel] 分析卡片面板初始化完成');
    }

    /**
     * 更新显式分析卡片数据。
     * `processingTimeMs` 目前由调用方保留传入，但渲染器本身不直接消费它。
     */
    updateCards(data, status, processingTimeMs) { // eslint-disable-line no-unused-vars
        if (!this.container || !data) {
            return;
        }

        if (!this._isAnalysisData(data)) {
            this.container.innerHTML = '<p class="empty-text">无显式分析数据</p>';
            return;
        }

        this._renderAnalysisData(this._normalizeAnalysisData(data), status);
    }

    /**
     * 保留该 API 以兼容外部调用；显式 `analysisData` 渲染不再依赖 flowContext。
     */
    setFlowContext(flowData) { // eslint-disable-line no-unused-vars
    }

    clear() {
        if (!this.container) {
            return;
        }

        this.container.innerHTML = '<p class="empty-text">等待检测结果...</p>';
    }

    _isAnalysisData(data) {
        return !!data
            && typeof data === 'object'
            && (Array.isArray(data.cards) || Array.isArray(data.Cards));
    }

    _normalizeAnalysisData(data) {
        if (!data || typeof data !== 'object') {
            return { version: 1, cards: [] };
        }

        return {
            version: data.version ?? data.Version ?? 1,
            cards: Array.isArray(data.cards) ? data.cards : (Array.isArray(data.Cards) ? data.Cards : []),
            summary: data.summary ?? data.Summary ?? null
        };
    }

    _renderAnalysisData(analysisData, fallbackStatus) {
        const cards = Array.isArray(analysisData?.cards) ? [...analysisData.cards] : [];
        if (cards.length === 0) {
            this.container.innerHTML = '<p class="empty-text">无分析数据</p>';
            return;
        }

        cards.sort((left, right) => {
            const leftPriority = Number(left?.priority ?? 0);
            const rightPriority = Number(right?.priority ?? 0);
            return rightPriority - leftPriority;
        });

        this.container.innerHTML = cards
            .map(card => this._renderAnalysisCard(card, fallbackStatus))
            .join('');

        this._bindToggleEvents();
    }

    _renderAnalysisCard(card, fallbackStatus) {
        switch (String(card?.category || 'generic').toLowerCase()) {
            case 'measurement':
                return this._renderStructuredMeasurementCard(card, fallbackStatus);
            case 'recognition':
                return this._renderStructuredRecognitionCard(card, fallbackStatus);
            case 'diagnostic':
                return renderDiagnosticCardHtml(card, fallbackStatus);
            default:
                return this._renderStructuredGenericCard(card, fallbackStatus);
        }
    }

    _renderStructuredMeasurementCard(card, fallbackStatus) {
        const fields = Array.isArray(card?.fields) ? card.fields : [];
        const cardStatus = card?.status || fallbackStatus || 'OK';
        const rows = fields.map(field => {
            const numericValue = typeof field?.value === 'number'
                ? field.value
                : Number.parseFloat(field?.value);
            const displayValue = Number.isFinite(numericValue)
                ? (Number.isInteger(numericValue) ? numericValue : numericValue.toFixed(2))
                : (field?.value ?? '--');
            const unit = field?.unit || '';
            const icon = MEASUREMENT_ICON_MAP[this._normalizeLookupKey(field?.key)]
                || MEASUREMENT_ICON_MAP[this._normalizeLookupKey(field?.label)]
                || ICONS.ruler;
            const fieldStatus = field?.status || cardStatus;
            const isNG = fieldStatus === 'NG' || fieldStatus === 'Error';
            const statusBadge = isNG
                ? `<span class="ac-badge ac-badge-ng" title="NG">${ICONS.cross}</span>`
                : `<span class="ac-badge ac-badge-ok" title="OK">${ICONS.check}</span>`;

            return `
                <div class="ac-measurement-row">
                    <div class="ac-measurement-header">
                        <span class="ac-measurement-icon">${icon}</span>
                        <span class="ac-measurement-label">${this._escapeHtml(field?.label || this._toDisplayName(field?.key || ''))}</span>
                        ${statusBadge}
                    </div>
                    <div class="ac-measurement-value ${isNG ? 'ng' : 'ok'}">
                        <span class="ac-big-number">${this._escapeHtml(String(displayValue))}</span>
                        <span class="ac-unit">${this._escapeHtml(unit)}</span>
                    </div>
                </div>
            `;
        }).join('');

        return this._wrapCard(
            'measurement',
            ICONS.ruler,
            card?.title || '测量结果',
            rows || '<p class="empty-text">无测量字段</p>',
            this._toCardStatusClass(cardStatus)
        );
    }

    _renderStructuredRecognitionCard(card, fallbackStatus) {
        const fields = Array.isArray(card?.fields) ? card.fields : [];
        const cardStatus = card?.status || fallbackStatus || 'OK';
        const normalizedFields = fields.map(field => ({
            ...field,
            normalizedKey: this._normalizeLookupKey(field?.key)
        }));
        const textField = normalizedFields.find(field => field.normalizedKey === 'text' || field.normalizedKey === 'recognizedtext');
        const codeTypeField = normalizedFields.find(field => field.normalizedKey === 'codetype');
        const confidence = Number(card?.meta?.confidence ?? card?.meta?.Confidence ?? NaN);
        const confidencePct = Number.isFinite(confidence)
            ? (confidence > 1 ? confidence : confidence * 100)
            : null;
        const displayText = textField?.value ?? '--';
        const isNG = cardStatus === 'NG' || cardStatus === 'Error';
        const statusBadge = isNG
            ? `<span class="ac-badge ac-badge-ng" title="NG">${ICONS.cross}</span>`
            : `<span class="ac-badge ac-badge-ok" title="OK">${ICONS.check}</span>`;
        const confidenceHtml = confidencePct === null ? '' : `
            <div class="ac-ocr-confidence">
                <div class="ac-ocr-conf-header">
                    <span class="ac-ocr-conf-label">置信度</span>
                    <span class="ac-ocr-conf-value ${confidencePct > 90 ? 'high' : confidencePct > 70 ? 'medium' : 'low'}">${confidencePct.toFixed(1)}%</span>
                </div>
                <div class="ac-progress-bar">
                    <div class="ac-progress-fill ${confidencePct > 90 ? 'high' : confidencePct > 70 ? 'medium' : 'low'}" style="width: ${Math.min(confidencePct, 100)}%"></div>
                </div>
            </div>
        `;

        return this._wrapCard(
            'ocr',
            codeTypeField?.value ? ICONS.barcode : ICONS.ocr,
            card?.title || '识别结果',
            `
                <div class="ac-ocr-section">
                    <div class="ac-ocr-header">
                        <span class="ac-ocr-type-label">${this._escapeHtml(codeTypeField?.value ? `识别结果 (${codeTypeField.value})` : '识别结果')}</span>
                        ${statusBadge}
                    </div>
                    <div class="ac-ocr-text-box ${isNG ? 'ng' : 'ok'}">
                        <span class="ac-ocr-text">${this._escapeHtml(String(displayText))}</span>
                        ${!isNG ? `<span class="ac-ocr-check">${ICONS.check}</span>` : `<span class="ac-ocr-cross">${ICONS.cross}</span>`}
                    </div>
                    ${confidenceHtml}
                </div>
            `,
            this._toCardStatusClass(cardStatus)
        );
    }

    _renderStructuredGenericCard(card, fallbackStatus) {
        const fields = Array.isArray(card?.fields) ? card.fields : [];
        const rows = fields.map(field => {
            const rawValue = field?.value;
            const displayValue = typeof rawValue === 'object' && rawValue !== null
                ? this._escapeHtml(JSON.stringify(rawValue))
                : this._escapeHtml(String(rawValue ?? '--'));

            return `
                <div class="ac-generic-row">
                    <span class="ac-generic-key">${this._escapeHtml(field?.label || this._toDisplayName(field?.key || ''))}</span>
                    <span class="ac-generic-value">${displayValue}</span>
                </div>
            `;
        }).join('');

        return this._wrapCard(
            'generic',
            this._iconForCategory(card?.category),
            card?.title || '分析结果',
            rows || '<p class="empty-text">无分析字段</p>',
            this._toCardStatusClass(card?.status || fallbackStatus || 'OK')
        );
    }

    _iconForCategory(category) {
        switch (String(category || '').toLowerCase()) {
            case 'measurement':
                return ICONS.ruler;
            case 'recognition':
                return ICONS.ocr;
            case 'defect':
                return ICONS.defect;
            case 'match':
                return ICONS.match;
            case 'classification':
                return ICONS.target;
            default:
                return ICONS.generic;
        }
    }

    _toCardStatusClass(status) {
        return status === 'NG' || status === 'Error' ? 'ng' : 'ok';
    }

    _normalizeLookupKey(value) {
        return String(value || '').trim().toLowerCase();
    }

    _wrapCard(type, icon, title, content, statusClass = '') {
        return `
            <div class="ac-card ac-card-${type} ${statusClass ? 'ac-status-' + statusClass : ''}" data-card-type="${type}">
                <div class="ac-card-header">
                    <span class="ac-card-icon">${icon}</span>
                    <span class="ac-card-title">${this._escapeHtml(title)}</span>
                    <button class="ac-card-toggle" title="折叠/展开">
                        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                            <path d="M7 10l5 5 5-5z"/>
                        </svg>
                    </button>
                </div>
                <div class="ac-card-body">
                    ${content}
                </div>
            </div>
        `;
    }

    _bindToggleEvents() {
        this.container.querySelectorAll('.ac-card-toggle').forEach(btn => {
            btn.addEventListener('click', () => {
                const card = btn.closest('.ac-card');
                if (!card) {
                    return;
                }

                card.classList.toggle('collapsed');
                const icon = btn.querySelector('svg');
                if (icon) {
                    icon.style.transform = card.classList.contains('collapsed') ? 'rotate(180deg)' : '';
                }
            });
        });
    }

    _toDisplayName(key) {
        const nameMap = {
            Distance: '距离',
            Radius: '半径',
            Angle: '角度',
            Length: '长度',
            Area: '面积',
            Perimeter: '周长',
            Diameter: '直径',
            Width: '宽度',
            Height: '高度',
            Text: '识别文本',
            CodeType: '码制类型',
            CodeCount: '识别数量',
            DefectCount: '缺陷数量',
            BlobCount: 'Blob数量',
            ObjectCount: '目标数量',
            Objects: '目标列表',
            Score: '匹配分数',
            IsMatch: '匹配结果',
            Position: '位置',
            Confidence: '置信度',
            CircleCount: '圆数量',
            LineCount: '直线数量',
            ContourCount: '轮廓数量',
            OutputImage: '输出图像',
            Result: '结果',
            Output: '数据输出'
        };
        return nameMap[key] || key;
    }

    _escapeHtml(text) {
        return escapeHtml(text);
    }

    dispose() {
        this.container = null;
    }
}

export { AnalysisCardsPanel };
export default AnalysisCardsPanel;
