/**
 * 分析卡片面板组件
 * 根据算子输出类型动态渲染不同风格的分析卡片
 * 作者：蘅芜君
 */

// ================================
// 端口名 → 卡片类型映射表
// ================================
const MEASUREMENT_KEYS = new Set([
    'Distance', 'distance', 'Radius', 'radius', 'Angle', 'angle',
    'Length', 'length', 'Area', 'area', 'Perimeter', 'perimeter',
    'Diameter', 'diameter', 'Width', 'width', 'Height', 'height'
]);

const OCR_CODE_KEYS = new Set([
    'Text', 'text', 'CodeType', 'codeType', 'CodeCount', 'codeCount',
    'RecognizedText', 'recognizedText', 'OcrResult', 'ocrResult'
]);

const MEASUREMENT_OPERATOR_TYPES = new Set([
    'Measurement', 'WidthMeasurement', 'GapMeasurement', 'AngleMeasurement',
    'CircleMeasurement', 'LineMeasurement', 'ContourMeasurement', 'GeoMeasurement',
    'CaliperTool', 'PointLineDistance', 'LineLineDistance', 'ArcCaliper',
    'GeometricTolerance', 'PixelToWorldTransform', 'CoordinateTransform',
    'MinEnclosingGeometry', 'ColorMeasurement'
]);

const RECOGNITION_OPERATOR_TYPES = new Set([
    'OcrRecognition', 'CodeRecognition'
]);

const DEFECT_KEYS = new Set([
    'Defects', 'defects', 'DefectCount', 'defectCount',
    'BlobCount', 'blobCount', 'DetectionResults', 'detectionResults'
]);

const OBJECT_DETECTION_KEYS = new Set([
    'Objects', 'objects', 'ObjectCount', 'objectCount'
]);

const MATCH_KEYS = new Set([
    'Score', 'score', 'IsMatch', 'isMatch', 'Position', 'position',
    'MatchScore', 'matchScore', 'Confidence', 'confidence'
]);

// 跳过的键（图像数据不在卡片中展示）
const SKIP_KEYS = new Set(['Image', 'image', 'OutputImage', 'outputImage']);

const TECHNICAL_KEYS = new Set([
    'Format', 'format', 'SaveToFile', 'saveToFile',
    'Output', 'output', 'FilePath', 'filePath',
    'SaveError', 'saveError',
    'ImageWidth', 'imageWidth', 'ImageHeight', 'imageHeight'
]);

const AMBIGUOUS_MEASUREMENT_KEYS = new Set([
    'Width', 'width', 'Height', 'height',
    'Area', 'area', 'Perimeter', 'perimeter'
]);

const EXPLICIT_MEASUREMENT_SIGNAL_KEYS = new Set([
    'Distance', 'distance', 'Radius', 'radius', 'Angle', 'angle',
    'Length', 'length', 'Diameter', 'diameter',
    'MinWidth', 'minWidth', 'MaxWidth', 'maxWidth',
    'MinDistance', 'minDistance', 'MaxDistance', 'maxDistance',
    'ImageWidth', 'imageWidth', 'ImageHeight', 'imageHeight',
    'Direction', 'direction',
    'SampleCount', 'sampleCount', 'RefinedSampleCount', 'refinedSampleCount',
    'ContourCount', 'contourCount', 'Contours', 'contours',
    'CircleCount', 'circleCount', 'LineCount', 'lineCount'
]);

const EXPLICIT_RECOGNITION_SIGNAL_KEYS = new Set([
    'CodeType', 'codeType', 'CodeCount', 'codeCount',
    'RecognizedText', 'recognizedText', 'OcrResult', 'ocrResult',
    'IsSuccess', 'isSuccess', 'CodeResults', 'codeResults',
    'Codes', 'codes'
]);

const EXPORT_HINT_KEYS = new Set([
    'Format', 'format', 'SaveToFile', 'saveToFile',
    'Output', 'output', 'FilePath', 'filePath',
    'SaveError', 'saveError'
]);

// 缺陷检测场景下的从属字段（检测框属性）
// 当 outputData 中同时存在 DEFECT_KEYS 时，这些键不应独立归入测量/匹配卡片
const DETECTION_CONTEXT_SUBORDINATES = new Set([
    'Width', 'width', 'Height', 'height',
    'Confidence', 'confidence',
    'Area', 'area',
    'X', 'x', 'Y', 'y',
    'CenterX', 'centerX', 'CenterY', 'centerY'
]);

// 测量单位映射
const UNIT_MAP = {
    'Distance': 'mm', 'distance': 'mm',
    'Radius': 'mm', 'radius': 'mm',
    'Diameter': 'mm', 'diameter': 'mm',
    'Length': 'mm', 'length': 'mm',
    'Width': 'mm', 'width': 'mm',
    'Height': 'mm', 'height': 'mm',
    'Area': 'mm²', 'area': 'mm²',
    'Perimeter': 'mm', 'perimeter': 'mm',
    'Angle': '°', 'angle': '°'
};

// ================================
// 专业 SVG 图标表
// ================================
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
    match: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 3h18v18H3z"></path><path d="M12 8v8"></path><path d="M8 12h8"></path></svg>`,
    generic: `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="8" y1="6" x2="21" y2="6"></line><line x1="8" y1="12" x2="21" y2="12"></line><line x1="8" y1="18" x2="21" y2="18"></line><line x1="3" y1="6" x2="3.01" y2="6"></line><line x1="3" y1="12" x2="3.01" y2="12"></line><line x1="3" y1="18" x2="3.01" y2="18"></line></svg>`,
    check: `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>`,
    cross: `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>`
};

// 测量图标映射
const MEASUREMENT_ICON_MAP = {
    'Distance': ICONS.distance, 'distance': ICONS.distance,
    'Radius': ICONS.radius, 'radius': ICONS.radius,
    'Diameter': ICONS.radius, 'diameter': ICONS.radius,
    'Angle': ICONS.angle, 'angle': ICONS.angle,
    'Length': ICONS.ruler, 'length': ICONS.ruler,
    'Area': ICONS.area, 'area': ICONS.area,
    'Perimeter': ICONS.area, 'perimeter': ICONS.area,
    'Width': ICONS.ruler, 'width': ICONS.ruler,
    'Height': ICONS.ruler, 'height': ICONS.ruler
};

class AnalysisCardsPanel {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        this.cards = [];
        this.lastOutputData = null;
        this.flowContext = this._buildFlowContext(null);

        if (!this.container) {
            console.warn('[AnalysisCardsPanel] 找不到容器:', containerId);
        }

        console.log('[AnalysisCardsPanel] 分析卡片面板初始化完成');
    }

    /**
     * 更新卡片数据
     * @param {Object} outputData - 算子输出数据字典
     * @param {string} status - 检测状态 OK/NG/Error
     * @param {number} [processingTimeMs] - 处理耗时
     */
    updateCards(data, status, processingTimeMs) {
        if (!this.container || !data) return;

        this.lastOutputData = data;

        if (this._isAnalysisData(data)) {
            this._renderAnalysisData(this._normalizeAnalysisData(data), status);
            return;
        }

        this.container.innerHTML = '<p class="empty-text">无显式分析数据</p>';
    }

    setFlowContext(flowData) {
        this.flowContext = this._buildFlowContext(flowData);
    }

    /**
     * 清空卡片
     */
    clear() {
        if (!this.container) return;
        this.lastOutputData = null;
        this.cards = [];
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
        const category = String(card?.category || 'generic').toLowerCase();
        switch (category) {
            case 'measurement':
                return this._renderStructuredMeasurementCard(card, fallbackStatus);
            case 'recognition':
                return this._renderStructuredRecognitionCard(card, fallbackStatus);
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
            const icon = MEASUREMENT_ICON_MAP[this._normalizeFieldLookupKey(field?.key)]
                || MEASUREMENT_ICON_MAP[this._normalizeFieldLookupKey(field?.label)]
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
            normalizedKey: String(field?.key || '').toLowerCase()
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
            default:
                return ICONS.generic;
        }
    }

    _toCardStatusClass(status) {
        if (status === 'NG' || status === 'Error') {
            return 'ng';
        }

        return 'ok';
    }

    _normalizeFieldLookupKey(value) {
        const text = String(value || '').trim();
        if (!text) {
            return '';
        }

        return text.charAt(0).toUpperCase() + text.slice(1);
    }

    // ================================
    // 数据分类
    // ================================

    /**
     * 将 outputData 分类为不同的卡片组
     * 【修复】引入上下文感知：当存在缺陷检测上下文时，
     * 检测框属性字段（Width/Height/Confidence/Area等）不再被错误归类为测量/匹配卡片
     */
    _classifyOutputData(outputData, status, processingTimeMs) {
        const groups = {
            measurements: [],   // 测量数据
            ocrCodes: [],       // OCR/条码
            defects: [],        // 缺陷
            objectDetections: [], // 目标检测
            matches: [],        // 匹配
            generic: []         // 通用
        };

        // 上下文感知：预扫描是否存在缺陷检测/目标检测场景
        const keys = Object.keys(outputData);
        const hasDefectContext = keys.some(k => DEFECT_KEYS.has(k));
        const hasObjectContext = keys.some(k => OBJECT_DETECTION_KEYS.has(k));
        const hasExplicitMeasurementSignals = keys.some(k => EXPLICIT_MEASUREMENT_SIGNAL_KEYS.has(k));
        const hasExplicitRecognitionSignals = keys.some(k => EXPLICIT_RECOGNITION_SIGNAL_KEYS.has(k));

        for (const [key, value] of Object.entries(outputData)) {
            // 跳过图像和超长 base64 字符串
            if (SKIP_KEYS.has(key)) continue;
            if (TECHNICAL_KEYS.has(key)) continue;
            if (typeof value === 'string' && value.length > 500) continue;

            // 上下文感知：在缺陷/目标检测场景中，检测框属性字段不应独立归入其他卡片
            if ((hasDefectContext || hasObjectContext) && DETECTION_CONTEXT_SUBORDINATES.has(key)) {
                continue;
            }

            if (this._isImageDimensionMetadata(key, outputData, hasExplicitMeasurementSignals)) {
                continue;
            }

            if ((key === 'Text' || key === 'text')
                && this._isStructuredExportText(value, outputData)
                && !hasExplicitRecognitionSignals) {
                continue;
            }

            if (this._shouldClassifyAsMeasurement(key, outputData, hasExplicitMeasurementSignals)) {
                groups.measurements.push({ key, value, status });
            } else if (this._shouldClassifyAsRecognition(key, value, outputData, hasExplicitRecognitionSignals)) {
                groups.ocrCodes.push({ key, value, status });
            } else if (DEFECT_KEYS.has(key)) {
                groups.defects.push({ key, value, status });
            } else if (OBJECT_DETECTION_KEYS.has(key)) {
                groups.objectDetections.push({ key, value, status });
            } else if ((key === 'DetectionList' || key === 'detectionList') && hasObjectContext) {
                // 目标检测模式下，兼容通用输出键 DetectionList
                groups.objectDetections.push({ key, value, status });
            } else if ((key === 'DetectionList' || key === 'detectionList') && hasDefectContext) {
                // 缺陷检测模式下，兼容通用输出键 DetectionList
                groups.defects.push({ key, value, status });
            } else if (MATCH_KEYS.has(key)) {
                groups.matches.push({ key, value, status });
            } else {
                groups.generic.push({ key, value, status });
            }
        }

        return groups;
    }

    // ================================
    // 卡片渲染
    // ================================

    _renderCards(groups) {
        const fragments = [];

        // 1. 测量卡片
        if (groups.measurements.length > 0) {
            fragments.push(this._renderMeasurementCard(groups.measurements));
        }

        // 2. OCR/条码卡片
        if (groups.ocrCodes.length > 0) {
            fragments.push(this._renderOcrCodeCard(groups.ocrCodes));
        }

        // 3. 缺陷卡片
        if (groups.defects.length > 0) {
            fragments.push(this._renderDefectCard(groups.defects));
        }

        // 4. 目标检测卡片
        if (groups.objectDetections.length > 0) {
            fragments.push(this._renderObjectDetectionCard(groups.objectDetections));
        }

        // 5. 匹配卡片
        if (groups.matches.length > 0) {
            fragments.push(this._renderMatchCard(groups.matches));
        }

        // 6. 通用数据卡片
        if (groups.generic.length > 0) {
            fragments.push(this._renderGenericCard(groups.generic));
        }

        if (fragments.length === 0) {
            this.container.innerHTML = '<p class="empty-text">无分析数据</p>';
            return;
        }

        this.container.innerHTML = fragments.join('');

        // 绑定折叠/展开事件
        this._bindToggleEvents();
    }

    /**
     * 测量卡片 — 距离/角度/面积等
     */
    _renderMeasurementCard(items) {
        const rows = items.map(item => {
            const val = typeof item.value === 'number' ? item.value : parseFloat(item.value);
            const displayVal = isNaN(val) ? item.value : (Number.isInteger(val) ? val : val.toFixed(2));
            const unit = UNIT_MAP[item.key] || '';
            const icon = MEASUREMENT_ICON_MAP[item.key] || ICONS.ruler;
            const status = item.status || 'OK';
            const isNG = status === 'NG' || status === 'Error';
            const statusBadge = isNG
                ? `<span class="ac-badge ac-badge-ng" title="NG">${ICONS.cross}</span>`
                : `<span class="ac-badge ac-badge-ok" title="OK">${ICONS.check}</span>`;

            const rangeBarHtml = typeof val === 'number' ? this._renderRangeBar(item, val, isNG) : '';

            return `
                <div class="ac-measurement-row">
                    <div class="ac-measurement-header">
                        <span class="ac-measurement-icon">${icon}</span>
                        <span class="ac-measurement-label">${this._escapeHtml(this._toDisplayName(item.key))}</span>
                        ${statusBadge}
                    </div>
                    <div class="ac-measurement-value ${isNG ? 'ng' : 'ok'}">
                        <span class="ac-big-number">${displayVal}</span>
                        <span class="ac-unit">${unit}</span>
                    </div>
                    ${rangeBarHtml}
                </div>
            `;
        }).join('');

        return this._wrapCard('measurement', ICONS.ruler, '距离测量', rows);
    }

    /**
     * OCR/条码卡片
     */
    _renderOcrCodeCard(items) {
        const textItem = items.find(i =>
            i.key === 'Text' ||
            i.key === 'text' ||
            i.key === 'RecognizedText' ||
            i.key === 'recognizedText' ||
            i.key === 'OcrResult' ||
            i.key === 'ocrResult'
        );
        const codeTypeItem = items.find(i => i.key === 'CodeType' || i.key === 'codeType');
        
        const text = textItem ? textItem.value : '--';
        const codeType = codeTypeItem ? codeTypeItem.value : '';
        const status = textItem ? textItem.status : 'OK';
        const isNG = status === 'NG';
        const statusBadge = isNG
            ? `<span class="ac-badge ac-badge-ng" title="NG">${ICONS.cross}</span>`
            : `<span class="ac-badge ac-badge-ok" title="OK">${ICONS.check}</span>`;

        // 模拟置信度（如果有 Score/Confidence 字段）
        const confidenceItem = items.find(i => i.key === 'Confidence' || i.key === 'confidence' || i.key === 'Score' || i.key === 'score');
        const hasConfidence = confidenceItem && typeof confidenceItem.value === 'number';
        const confidence = hasConfidence
            ? (confidenceItem.value > 1 ? confidenceItem.value : confidenceItem.value * 100)
            : null;

        const confidenceHtml = confidence === null ? `
            <div class="ac-ocr-confidence">
                <div class="ac-ocr-conf-header">
                    <span class="ac-ocr-conf-label">置信度</span>
                    <span class="ac-ocr-conf-value low">暂无数据</span>
                </div>
            </div>
        ` : `
            <div class="ac-ocr-confidence">
                <div class="ac-ocr-conf-header">
                    <span class="ac-ocr-conf-label">置信度</span>
                    <span class="ac-ocr-conf-value ${confidence > 90 ? 'high' : confidence > 70 ? 'medium' : 'low'}">${confidence.toFixed(1)}%</span>
                </div>
                <div class="ac-progress-bar">
                    <div class="ac-progress-fill ${confidence > 90 ? 'high' : confidence > 70 ? 'medium' : 'low'}" style="width: ${Math.min(confidence, 100)}%"></div>
                </div>
            </div>
        `;

        const charIndicatorHtml = typeof text === 'string' && text.length > 0 ? `
            <div class="ac-ocr-chars">
                <span class="ac-ocr-chars-label">字符分级</span>
                <div class="ac-ocr-char-bars">
                    ${text.split('').slice(0, 15).map(() =>
                        `<span class="ac-ocr-char-bar good"></span>`
                    ).join('')}
                </div>
                <span class="ac-badge ac-badge-ok" style="margin-left: auto;" title="PASS">${ICONS.check}</span>
            </div>
        ` : '';

        const content = `
            <div class="ac-ocr-section">
                <div class="ac-ocr-header">
                    <span class="ac-ocr-type-label">识别结果${codeType ? ` (${this._escapeHtml(codeType)})` : ''}</span>
                    ${statusBadge}
                </div>
                <div class="ac-ocr-text-box ${isNG ? 'ng' : 'ok'}">
                    <span class="ac-ocr-text">${this._escapeHtml(String(text))}</span>
                    ${!isNG ? `<span class="ac-ocr-check">${ICONS.check}</span>` : `<span class="ac-ocr-cross">${ICONS.cross}</span>`}
                </div>
                ${confidenceHtml}
                ${charIndicatorHtml}
            </div>
        `;

        const title = codeType ? '条码识别' : 'OCR 文本识别';
        const icon = codeType ? ICONS.barcode : ICONS.ocr;

        return this._wrapCard('ocr', icon, title, content, isNG ? 'ng' : 'ok');
    }

    /**
     * 缺陷检测卡片
     */
    _renderDefectCard(items) {
        const defectsItem = items.find(i =>
            i.key === 'Defects' ||
            i.key === 'defects' ||
            i.key === 'DetectionResults' ||
            i.key === 'detectionResults' ||
            i.key === 'DetectionList' ||
            i.key === 'detectionList'
        );
        const countItem = items.find(i => i.key === 'DefectCount' || i.key === 'defectCount' || i.key === 'BlobCount' || i.key === 'blobCount');

        const defects = defectsItem ? this._extractDetectionArray(defectsItem.value) : [];

        const count = countItem ? countItem.value : (Array.isArray(defects) ? defects.length : 0);
        const isNG = count > 0;

        const defectListHtml = defects.length > 0 ? defects.slice(0, 5).map(d => {
            const name = d.className || d.ClassName || d.type || d.label || d.Label || d.description || '未知缺陷';
            const conf = d.confidence || d.Confidence || d.confidenceScore || d.score;
            const confText = typeof conf === 'number'
                ? `${(conf > 1 ? conf : conf * 100).toFixed(1)}%`
                : '';

            return `
                <div class="ac-defect-item">
                    <span class="ac-defect-dot ${isNG ? 'ng' : 'ok'}"></span>
                    <span class="ac-defect-name">${this._escapeHtml(name)}</span>
                    ${confText ? `<span class="ac-defect-conf">${confText}</span>` : ''}
                </div>
            `;
        }).join('') : `<p class="empty-text" style="margin: 8px 0; font-size: 12px; color: var(--text-secondary); display: flex; align-items: center; gap: 4px;">${ICONS.check} 表面完好无缺陷</p>`;

        const content = `
            <div class="ac-defect-summary">
                <span class="ac-defect-count-label">检出数量</span>
                <span class="ac-defect-count ${isNG ? 'ng' : 'ok'}">${count}</span>
            </div>
            <div class="ac-defect-list">
                ${defectListHtml}
            </div>
        `;

        return this._wrapCard('defect', ICONS.defect, '缺陷检测', content, isNG ? 'ng' : 'ok');
    }

    /**
     * 目标检测卡片
     */
    _renderObjectDetectionCard(items) {
        const objectsItem = items.find(i =>
            i.key === 'Objects' ||
            i.key === 'objects' ||
            i.key === 'DetectionList' ||
            i.key === 'detectionList'
        );
        const countItem = items.find(i => i.key === 'ObjectCount' || i.key === 'objectCount');

        const objects = objectsItem ? this._extractDetectionArray(objectsItem.value) : [];

        const fallbackCount = Array.isArray(objects) ? objects.length : 0;
        const countRaw = countItem ? Number(countItem.value) : fallbackCount;
        const count = Number.isFinite(countRaw) ? countRaw : fallbackCount;

        const objectListHtml = objects.length > 0 ? objects.slice(0, 8).map(obj => {
            const name = obj.label || obj.Label || obj.className || obj.ClassName || obj.type || obj.description || '未知目标';
            const conf = obj.confidence || obj.Confidence || obj.confidenceScore || obj.score;
            const confText = typeof conf === 'number'
                ? `${(conf > 1 ? conf : conf * 100).toFixed(1)}%`
                : '';

            return `
                <div class="ac-defect-item">
                    <span class="ac-defect-dot ok"></span>
                    <span class="ac-defect-name">${this._escapeHtml(String(name))}</span>
                    ${confText ? `<span class="ac-defect-conf">${confText}</span>` : ''}
                </div>
            `;
        }).join('') : `<p class="empty-text" style="margin: 8px 0; font-size: 12px; color: var(--text-secondary);">未检出目标</p>`;

        const content = `
            <div class="ac-defect-summary">
                <span class="ac-defect-count-label">检出数量</span>
                <span class="ac-defect-count ok">${count}</span>
            </div>
            <div class="ac-defect-list">
                ${objectListHtml}
            </div>
        `;

        // 目标检测模式下，检出目标不代表 NG，卡片保持 OK 视觉。
        return this._wrapCard('object', ICONS.target, '目标检测', content, 'ok');
    }

    /**
     * 匹配卡片
     */
    _renderMatchCard(items) {
        const scoreItem = items.find(i => i.key === 'Score' || i.key === 'score' || i.key === 'MatchScore' || i.key === 'Confidence');
        const isMatchItem = items.find(i => i.key === 'IsMatch' || i.key === 'isMatch');
        const positionItem = items.find(i => i.key === 'Position' || i.key === 'position');

        const score = scoreItem ? (typeof scoreItem.value === 'number' ?
            (scoreItem.value > 1 ? scoreItem.value : scoreItem.value * 100) : parseFloat(scoreItem.value)) : null;
        const isMatch = isMatchItem ? isMatchItem.value : (score !== null ? score > 50 : null);
        
        const scoreHtml = score !== null ? `
            <div class="ac-match-score-row">
                <div class="ac-match-score-label">分数</div>
                <div class="ac-match-score-value ${score > 80 ? 'high' : score > 50 ? 'medium' : 'low'}">
                    ${score.toFixed(1)}%
                </div>
            </div>
        ` : '';

        const posHtml = positionItem ? `
            <div class="ac-match-position-row">
                <div class="ac-match-score-label">位置</div>
                <div class="ac-match-position-val">${typeof positionItem.value === 'object' ? `X:${positionItem.value.X || positionItem.value.x || 0}, Y:${positionItem.value.Y || positionItem.value.y || 0}` : this._escapeHtml(String(positionItem.value))}</div>
            </div>
        ` : '';

        const content = `
            <div class="ac-match-status-badge ${isMatch ? 'matched' : 'unmatched'}">
                ${isMatch ? ICONS.check : ICONS.cross}
            </div>
            <div class="ac-match-details">
                ${scoreHtml}
                ${posHtml}
            </div>
        `;
        return this._wrapCard('match', ICONS.match, '模板匹配', content, isMatch === false ? 'ng' : 'ok');
    }

    /**
     * 通用数据卡片 — 键值对
     */
    _renderGenericCard(items) {
        const rows = items.map(item => {
            let displayValue;
            if (typeof item.value === 'boolean') {
                displayValue = item.value
                    ? `<span class="ac-bool-true" style="display:inline-flex;align-items:center;color:var(--text-secondary);">${ICONS.check}</span>`
                    : `<span class="ac-bool-false" style="display:inline-flex;align-items:center;color:var(--text-secondary);">${ICONS.cross}</span>`;
            } else if (typeof item.value === 'number') {
                displayValue = Number.isInteger(item.value) ? item.value : item.value.toFixed(4);
            } else if (typeof item.value === 'object') {
                displayValue = `<code class="ac-json-code">${this._escapeHtml(JSON.stringify(item.value).substring(0, 80))}</code>`;
            } else {
                displayValue = this._escapeHtml(String(item.value).substring(0, 100));
            }

            return `
                <div class="ac-generic-row">
                    <span class="ac-generic-key">${this._escapeHtml(this._toDisplayName(item.key))}</span>
                    <span class="ac-generic-value">${displayValue}</span>
                </div>
            `;
        }).join('');

        return this._wrapCard('generic', ICONS.generic, '数据输出', rows);
    }

    // ================================
    // 工具方法
    // ================================

    /**
     * 范围条渲染器
     */
    _renderRangeBar(item, value, isNG) {
        const minRaw = item?.min ?? item?.Min ?? item?.lowerBound ?? item?.LowerBound;
        const maxRaw = item?.max ?? item?.Max ?? item?.upperBound ?? item?.UpperBound;
        const min = Number.parseFloat(minRaw);
        const max = Number.parseFloat(maxRaw);

        if (!Number.isFinite(min) || !Number.isFinite(max) || max <= min) {
            return '';
        }

        const pct = Math.max(0, Math.min(100, ((value - min) / (max - min)) * 100));

        return `
            <div class="ac-range-bar-container">
                <div class="ac-range-bar">
                    <div class="ac-range-zone ok-zone"></div>
                    <div class="ac-range-pointer ${isNG ? 'ng' : 'ok'}" style="left: ${pct}%">
                        <span class="ac-range-pointer-diamond"></span>
                    </div>
                </div>
                <div class="ac-range-labels">
                    <span class="ac-range-min">${min.toFixed(1)}</span>
                    <span class="ac-range-max">${max.toFixed(1)}</span>
                </div>
            </div>
        `;
    }

    /**
     * 卡片外壳包装
     */
    /**
     * Flow semantic helpers
     */
    _buildFlowContext(flowData) {
        const operators = Array.isArray(flowData?.operators)
            ? flowData.operators
            : (Array.isArray(flowData?.Operators) ? flowData.Operators : []);
        const operatorTypes = new Set();

        for (const operator of operators) {
            const type = String(operator?.type ?? operator?.Type ?? '').trim();
            if (type) {
                operatorTypes.add(type);
            }
        }

        let hasMeasurementOperators = false;
        let hasRecognitionOperators = false;

        for (const type of operatorTypes) {
            if (!hasMeasurementOperators && MEASUREMENT_OPERATOR_TYPES.has(type)) {
                hasMeasurementOperators = true;
            }

            if (!hasRecognitionOperators && RECOGNITION_OPERATOR_TYPES.has(type)) {
                hasRecognitionOperators = true;
            }

            if (hasMeasurementOperators && hasRecognitionOperators) {
                break;
            }
        }

        return {
            hasFlowDefinition: operatorTypes.size > 0,
            hasMeasurementOperators,
            hasRecognitionOperators
        };
    }

    _shouldClassifyAsMeasurement(key, outputData, hasExplicitMeasurementSignals) {
        if (!MEASUREMENT_KEYS.has(key)) {
            return false;
        }

        if (this.flowContext?.hasFlowDefinition && !this.flowContext.hasMeasurementOperators) {
            return false;
        }

        if (AMBIGUOUS_MEASUREMENT_KEYS.has(key)) {
            return hasExplicitMeasurementSignals || !this._hasImagePayload(outputData);
        }

        return true;
    }

    _shouldClassifyAsRecognition(key, value, outputData, hasExplicitRecognitionSignals) {
        if (!OCR_CODE_KEYS.has(key)) {
            return false;
        }

        if (key === 'Text' || key === 'text') {
            if (typeof value !== 'string' || value.trim().length === 0) {
                return false;
            }

            if (this._isStructuredExportText(value, outputData) && !hasExplicitRecognitionSignals) {
                return false;
            }
        }

        if (this.flowContext?.hasFlowDefinition && !this.flowContext.hasRecognitionOperators) {
            return hasExplicitRecognitionSignals;
        }

        return true;
    }

    _hasImagePayload(outputData) {
        return Object.keys(outputData).some(key => SKIP_KEYS.has(key));
    }

    _isImageDimensionMetadata(key, outputData, hasExplicitMeasurementSignals) {
        if (!AMBIGUOUS_MEASUREMENT_KEYS.has(key)) {
            return false;
        }

        return this._hasImagePayload(outputData) && !hasExplicitMeasurementSignals;
    }

    _isStructuredExportText(value, outputData) {
        if (typeof value !== 'string') {
            return false;
        }

        const text = value.trim();
        if (!text) {
            return false;
        }

        const looksLikeStructuredPayload =
            (text.startsWith('{') && text.endsWith('}')) ||
            (text.startsWith('[') && text.endsWith(']'));
        if (!looksLikeStructuredPayload) {
            return false;
        }

        const hasExportHints = Object.keys(outputData).some(key => EXPORT_HINT_KEYS.has(key));
        if (hasExportHints) {
            return true;
        }

        return text.includes('\"Format\"')
            || text.includes('\"SaveToFile\"')
            || text.includes('\"ActualThreshold\"');
    }

    /**
     * Card shell wrapper
     */
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

    /**
     * 绑定折叠/展开
     */
    _bindToggleEvents() {
        this.container.querySelectorAll('.ac-card-toggle').forEach(btn => {
            btn.addEventListener('click', () => {
                const card = btn.closest('.ac-card');
                if (card) {
                    card.classList.toggle('collapsed');
                    const icon = btn.querySelector('svg');
                    if (icon) {
                        icon.style.transform = card.classList.contains('collapsed') ? 'rotate(180deg)' : '';
                    }
                }
            });
        });
    }

    /**
     * 端口名 → 显示名
     */
    _toDisplayName(key) {
        const nameMap = {
            'Distance': '距离', 'Radius': '半径', 'Angle': '角度',
            'Length': '长度', 'Area': '面积', 'Perimeter': '周长',
            'Diameter': '直径', 'Width': '宽度', 'Height': '高度',
            'Text': '识别文本', 'CodeType': '码制类型', 'CodeCount': '识别数量',
            'DefectCount': '缺陷数量', 'BlobCount': 'Blob数量',
            'ObjectCount': '目标数量', 'Objects': '目标列表',
            'Score': '匹配分数', 'IsMatch': '匹配结果', 'Position': '位置',
            'Confidence': '置信度', 'CircleCount': '圆数量',
            'LineCount': '直线数量', 'ContourCount': '轮廓数量',
            'OutputImage': '输出图像', 'Result': '结果', 'Output': '数据输出'
        };
        return nameMap[key] || key;
    }

    /**
     * HTML 转义
     */
    _escapeHtml(text) {
        if (text === null || text === undefined) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * 兼容多种检测结果结构，统一提取为数组
     */
    _extractDetectionArray(rawValue) {
        if (!rawValue) return [];

        if (Array.isArray(rawValue)) {
            return rawValue;
        }

        if (typeof rawValue === 'string') {
            try {
                return this._extractDetectionArray(JSON.parse(rawValue));
            } catch (e) {
                console.warn('解析检测列表 JSON 失败', e);
                return [];
            }
        }

        if (typeof rawValue === 'object') {
            if (Array.isArray(rawValue.Detections)) return rawValue.Detections;
            if (Array.isArray(rawValue.detections)) return rawValue.detections;
            if (Array.isArray(rawValue.Results)) return rawValue.Results;
            if (Array.isArray(rawValue.results)) return rawValue.results;
            if (Array.isArray(rawValue.Items)) return rawValue.Items;
            if (Array.isArray(rawValue.items)) return rawValue.items;
        }

        return [];
    }

    /**
     * 销毁
     */
    dispose() {
        this.container = null;
        this.cards = [];
        this.lastOutputData = null;
    }
}

export { AnalysisCardsPanel };
export default AnalysisCardsPanel;
