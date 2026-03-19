// src/features/calibration/calibrationWizard.js
import httpClient from '../../core/messaging/httpClient.js';

console.log("[CalibrationWizard] Module is loading...");

export class CalibrationWizard {
    constructor() {
        this.currentStep = 1;
        this.points = [];
        this.calibrationResult = null;
        
        // 我们需要截取图像组件提供的底图，或临时提供上传
        this.currentImageBase64 = null;
        
        this.init();
    }

    init() {
        console.log("[CalibrationWizard] constructor init(). readyState:", document.readyState);
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => {
                this.injectHtmlElement();
                this.bindEvents();
            });
        } else {
            this.injectHtmlElement();
            this.bindEvents();
        }
    }

    injectHtmlElement() {
        if (document.getElementById('calibration-modal-overlay')) return;

        const html = `
            <div id="calibration-modal-overlay" class="calibration-modal-overlay">
                <div class="calibration-modal">
                    <div class="calibration-modal-header">
                        <h3 class="calibration-modal-title">
                            <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
                                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm0-14c-3.31 0-6 2.69-6 6s2.69 6 6 6 6-2.69 6-6-2.69-6-6-6zm0 10c-2.21 0-4-1.79-4-4s1.79-4 4-4 4 1.79 4 4-1.79 4-4 4zm0-6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"/>
                            </svg>
                            手眼标定向导 (正交模型)
                        </h3>
                        <button id="btn-calib-close-top" class="btn-close-modal">
                            <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor"><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/></svg>
                        </button>
                    </div>
                    
                    <div class="calibration-stepper">
                        <div class="step-item active" id="calib-step-1">
                            <div class="step-circle">1</div>
                            <div class="step-label">采集标定点</div>
                        </div>
                        <div class="step-item" id="calib-step-2">
                            <div class="step-circle">2</div>
                            <div class="step-label">矩阵求解</div>
                        </div>
                        <div class="step-item" id="calib-step-3">
                            <div class="step-circle">3</div>
                            <div class="step-label">保存应用</div>
                        </div>
                    </div>

                    <div class="calibration-content">
                        <!-- Step 1 Panel -->
                        <div class="calibration-step-panel active" id="calib-panel-1">
                            <div class="step-1-layout">
                                <div class="step-1-left" id="calib-img-container">
                                    <div class="image-preview-placeholder" id="calib-img-placeholder">
                                        <svg viewBox="0 0 24 24" width="48" height="48" fill="currentColor"><path d="M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z"/></svg>
                                        <span>点击加载测试图像供取点，或在此处直接手动输入下方坐标列表</span>
                                        <input type="file" id="calib-file-input" accept="image/*" style="display:none;" />
                                        <button class="btn btn-secondary" onclick="document.getElementById('calib-file-input').click()">浏览图像...</button>
                                    </div>
                                    <img id="calib-image-preview" class="calibration-image-canvas" style="display:none;" draggable="false" />
                                </div>
                                <div class="step-1-right">
                                    <div class="point-input-card">
                                        <h4>录入映射点对</h4>
                                        <div class="coord-group">
                                            <div class="coord-field">
                                                <label>像素 X (Px)</label>
                                                <input type="number" id="calib-px" class="coord-input" step="0.1" />
                                            </div>
                                            <div class="coord-field">
                                                <label>像素 Y (Px)</label>
                                                <input type="number" id="calib-py" class="coord-input" step="0.1" />
                                            </div>
                                        </div>
                                        <div class="coord-group">
                                            <div class="coord-field">
                                                <label>机械臂/物理 X (mm)</label>
                                                <input type="number" id="calib-rx" class="coord-input" step="0.001" />
                                            </div>
                                            <div class="coord-field">
                                                <label>机械臂/物理 Y (mm)</label>
                                                <input type="number" id="calib-ry" class="coord-input" step="0.001" />
                                            </div>
                                        </div>
                                        <button id="btn-calib-add-point" class="btn btn-primary" style="width: 100%">+ 添加映射对</button>
                                    </div>
                                    
                                    <div class="points-list-container">
                                        <div class="points-list-header">
                                            <span>Pixel(X,Y)</span>
                                            <span>Phys(X,Y)</span>
                                            <span>操作</span>
                                        </div>
                                        <div class="points-list-body" id="calib-points-list">
                                            <!-- Dynamically inserted -->
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <!-- Step 2 Panel -->
                        <div class="calibration-step-panel" id="calib-panel-2">
                            <div class="step-2-layout">
                                <h3>二乘法正交解算结果</h3>
                                <div class="result-cards-grid">
                                    <div class="result-card">
                                        <div class="result-card-title">X 轴缩放及平移</div>
                                        <div class="matrix-display" id="calib-res-x">
                                            ScaleX: --<br/>
                                            OriginX: --
                                        </div>
                                    </div>
                                    <div class="result-card">
                                        <div class="result-card-title">Y 轴缩放及平移</div>
                                        <div class="matrix-display" id="calib-res-y">
                                            ScaleY: --<br/>
                                            OriginY: --
                                        </div>
                                    </div>
                                    <div class="result-card">
                                        <div class="result-card-title">X 轴均方误 (Mean Error)</div>
                                        <div class="result-card-value" id="calib-err-x">-- mm</div>
                                    </div>
                                    <div class="result-card">
                                        <div class="result-card-title">Y 轴均方误 (Mean Error)</div>
                                        <div class="result-card-value" id="calib-err-y">-- mm</div>
                                    </div>
                                </div>
                                <div style="color: var(--text-secondary); font-size: 0.875rem;">
                                    整体模型均方根误差 (RMSE): <span id="calib-rmse" style="color:#fff;font-weight:bold;">--</span> mm
                                </div>
                            </div>
                        </div>

                        <!-- Step 3 Panel -->
                        <div class="calibration-step-panel" id="calib-panel-3">
                            <div class="step-3-layout">
                                <div class="save-card">
                                    <h3 style="margin-top:0">保存并应用标定</h3>
                                    <p>已成功获取解算数据，此数据适用于 <code>CoordinateTransform</code> 算子作为其 <code>CalibrationFile</code> 参数。请输入文件名（将保存在系统 AppData 区域，或输入绝对路径使用）。</p>
                                    
                                    <div class="file-input-group">
                                        <label for="calib-filename">标定文件名</label>
                                        <input type="text" id="calib-filename" value="default_hand_eye.json" placeholder="例如：cam1_robot_calib.json" />
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="calibration-modal-footer">
                        <div class="error-message-box" id="calib-error-msg"></div>
                        <div style="display:flex; gap:0.5rem">
                            <button id="btn-calib-prev" class="btn btn-secondary" style="display:none">上一步</button>
                            <button id="btn-calib-next" class="btn btn-primary">下一步 (求解)</button>
                            <button id="btn-calib-finish" class="btn btn-success" style="display:none">保存</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        document.body.insertAdjacentHTML('beforeend', html);
    }

    bindEvents() {
        const d = document;
        
        // 主开关
        const btnOpen = d.getElementById('btn-calibration');
        console.log("[CalibrationWizard] Finding btn-calibration:", btnOpen);
        if (btnOpen) {
            btnOpen.addEventListener('click', (e) => {
                console.log("[CalibrationWizard] Calibration button clicked!");
                this.open();
            });
        }

        d.getElementById('btn-calib-close-top')?.addEventListener('click', () => this.close());

        // 步骤控制
        d.getElementById('btn-calib-next')?.addEventListener('click', () => this.nextStep());
        d.getElementById('btn-calib-prev')?.addEventListener('click', () => this.prevStep());
        d.getElementById('btn-calib-finish')?.addEventListener('click', () => this.finish());

        // 图像底图加载
        d.getElementById('calib-file-input')?.addEventListener('change', (e) => this.handleImageLoad(e));

        // 图像点击提取像素
        const imgCanvas = d.getElementById('calib-image-preview');
        if (imgCanvas) {
            imgCanvas.addEventListener('click', (e) => this.handleImageClick(e));
        }

        // 添加点位
        d.getElementById('btn-calib-add-point')?.addEventListener('click', () => this.addPoint());
    }

    open() {
        this.reset();
        document.getElementById('calibration-modal-overlay')?.classList.add('active');
    }

    close() {
        document.getElementById('calibration-modal-overlay')?.classList.remove('active');
    }

    reset() {
        this.currentStep = 1;
        this.points = [];
        this.calibrationResult = null;
        this.updateView();
        this.renderPoints();
        this.showError('');
    }

    handleImageLoad(e) {
        const file = e.target.files[0];
        if (!file) return;

        const reader = new FileReader();
        reader.onload = (evt) => {
            this.currentImageBase64 = evt.target.result;
            const imgEl = document.getElementById('calib-image-preview');
            imgEl.src = this.currentImageBase64;
            imgEl.style.display = 'block';
            document.getElementById('calib-img-placeholder').style.display = 'none';
        };
        reader.readAsDataURL(file);
    }

    handleImageClick(e) {
        const img = e.target;
        const rect = img.getBoundingClientRect();
        
        // 缩放修正
        const scaleX = img.naturalWidth / rect.width;
        const scaleY = img.naturalHeight / rect.height;

        const clickX = (e.clientX - rect.left) * scaleX;
        const clickY = (e.clientY - rect.top) * scaleY;

        document.getElementById('calib-px').value = clickX.toFixed(2);
        document.getElementById('calib-py').value = clickY.toFixed(2);
        
        // 焦点切到物理输入
        document.getElementById('calib-rx').focus();
    }

    addPoint() {
        const px = parseFloat(document.getElementById('calib-px').value);
        const py = parseFloat(document.getElementById('calib-py').value);
        const rx = parseFloat(document.getElementById('calib-rx').value);
        const ry = parseFloat(document.getElementById('calib-ry').value);

        if (isNaN(px) || isNaN(py) || isNaN(rx) || isNaN(ry)) {
            this.showError('请输入有效的数字坐标');
            return;
        }

        this.points.push({ pixelX: px, pixelY: py, physicalX: rx, physicalY: ry });
        this.renderPoints();
        
        // 清空
        document.getElementById('calib-px').value = '';
        document.getElementById('calib-py').value = '';
        document.getElementById('calib-rx').value = '';
        document.getElementById('calib-ry').value = '';
        this.showError('');
    }

    removePoint(index) {
        this.points.splice(index, 1);
        this.renderPoints();
    }

    renderPoints() {
        const container = document.getElementById('calib-points-list');
        if (!container) return;

        container.innerHTML = this.points.map((p, idx) => `
            <div class="point-row">
                <span>${p.pixelX.toFixed(1)}, ${p.pixelY.toFixed(1)}</span>
                <span>${p.physicalX.toFixed(3)}, ${p.physicalY.toFixed(3)}</span>
                <button class="btn-remove-point" onclick="window.calibrationWizard.removePoint(${idx})">
                    <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg>
                </button>
            </div>
        `).join('');
    }

    async nextStep() {
        this.showError('');

        if (this.currentStep === 1) {
            // 需要求解
            if (this.points.length < 2) {
                this.showError('至少需要输入 2 个以上的标定点对才能求解');
                return;
            }
            
            try {
                const result = await httpClient.post('/calibration/solve', this.points);
                if (!result.success && result.error) {
                    throw new Error(result.error);
                }
                
                this.calibrationResult = result;
                this.fillResultData();
                this.currentStep++;
                this.updateView();
            } catch (err) {
                this.showError(err.message || '求解失败，请检查点位分布或网络连接');
            }
        } 
        else if (this.currentStep === 2) {
            this.currentStep++;
            this.updateView();
        }
    }

    fillResultData() {
        if (!this.calibrationResult) return;
        const res = this.calibrationResult;
        
        document.getElementById('calib-res-x').innerHTML = `ScaleX: ${res.scaleX.toFixed(6)}<br/>OriginX: ${res.originX.toFixed(4)}`;
        document.getElementById('calib-res-y').innerHTML = `ScaleY: ${res.scaleY.toFixed(6)}<br/>OriginY: ${res.originY.toFixed(4)}`;
        
        const setVal = (id, val, thres) => {
            const el = document.getElementById(id);
            if(!el) return;
            el.textContent = val.toFixed(4) + ' mm';
            el.className = 'result-card-value ' + (val > thres ? 'error' : 'success');
        };

        setVal('calib-err-x', res.meanErrorX, 1.0);
        setVal('calib-err-y', res.meanErrorY, 1.0);
        
        document.getElementById('calib-rmse').textContent = res.rmse.toFixed(4);
    }

    prevStep() {
        if (this.currentStep > 1) {
            this.currentStep--;
            this.updateView();
            this.showError('');
        }
    }

    async finish() {
        const filename = document.getElementById('calib-filename').value.trim();
        if (!filename) {
            this.showError('必须输入要保存的文件名');
            return;
        }

        try {
            const req = {
                originX: this.calibrationResult.originX,
                originY: this.calibrationResult.originY,
                scaleX: this.calibrationResult.scaleX,
                scaleY: this.calibrationResult.scaleY,
                fileName: filename
            };

            const response = await httpClient.post('/calibration/save', req);
            
            // Success
            this.showError('');
            alert('标定文件已保存成功！');
            this.close();
            
        } catch (err) {
            this.showError(err.message || '保存失败');
        }
    }

    updateView() {
        // Update Step Toggles
        [1, 2, 3].forEach(step => {
            const item = document.getElementById(`calib-step-${step}`);
            const panel = document.getElementById(`calib-panel-${step}`);
            
            if (item) {
                item.classList.remove('active', 'completed');
                if (step < this.currentStep) item.classList.add('completed');
                else if (step === this.currentStep) item.classList.add('active');
            }
            if (panel) {
                panel.classList.remove('active');
                if (step === this.currentStep) panel.classList.add('active');
            }
        });

        // Update Footers
        const btnPrev = document.getElementById('btn-calib-prev');
        const btnNext = document.getElementById('btn-calib-next');
        const btnFin = document.getElementById('btn-calib-finish');

        if (this.currentStep === 1) {
            btnPrev.style.display = 'none';
            btnNext.style.display = 'block';
            btnNext.textContent = '下一步 (求解)';
            btnFin.style.display = 'none';
        } else if (this.currentStep === 2) {
            btnPrev.style.display = 'block';
            btnNext.style.display = 'block';
            btnNext.textContent = '下一步 (评估并通过)';
            btnFin.style.display = 'none';
        } else {
            btnPrev.style.display = 'block';
            btnNext.style.display = 'none';
            btnFin.style.display = 'block';
        }
    }

    showError(msg) {
        const el = document.getElementById('calib-error-msg');
        if (el) {
            el.textContent = msg;
            el.title = msg;
        }
    }
}

// Singleton instantiation
const wizard = new CalibrationWizard();
// Expose for inline handlers
window.calibrationWizard = wizard;
