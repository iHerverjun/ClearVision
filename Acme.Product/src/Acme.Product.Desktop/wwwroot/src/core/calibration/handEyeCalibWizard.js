/**
 * handEyeCalibWizard.js
 * 手眼标定三步向导组件
 *
 * Step 1: 采集标定点
 * Step 2: 验证与求解
 * Step 3: 保存标定文件
 */

export class HandEyeCalibWizard {
    constructor(cameraManager = null, options = {}) {
        this.cameraManager = cameraManager;
        this.captureFrame = typeof options.captureFrame === 'function' ? options.captureFrame : null;
        this.getCameraBindingId = typeof options.getCameraBindingId === 'function' ? options.getCameraBindingId : null;
        this.currentStep = 1;
        this.points = [];
        this.solveResult = null;
        this.overlay = null;
        this.els = null;
        this._boundRenderFrame = null;
        this._boundKeydown = null;
        this._cameraPreviewUrl = null;

        this.onWebMessageReceived = this.onWebMessageReceived.bind(this);

        this.createUI();
        this.attachEvents();
    }

    createUI() {
        this.overlay = document.createElement('div');
        this.overlay.className = 'calib-wizard-overlay';

        const modal = document.createElement('div');
        modal.className = 'calib-wizard-modal';

        modal.innerHTML = `
            <div class="calib-wizard-header">
                <div class="calib-wizard-title">
                    <span>🔡</span> 手眼标定向导 (N点平移缩放标定)
                </div>
                <button class="calib-wizard-close" type="button" id="calib-btn-close" aria-label="关闭">×</button>
            </div>
            <div class="calib-stepper">
                <div class="calib-step active" id="calib-step-1-indic">
                    <div class="calib-step-circle">1</div>
                    <div class="calib-step-label">采集标定点</div>
                </div>
                <div class="calib-step" id="calib-step-2-indic">
                    <div class="calib-step-circle">2</div>
                    <div class="calib-step-label">验证与求解</div>
                </div>
                <div class="calib-step" id="calib-step-3-indic">
                    <div class="calib-step-circle">3</div>
                    <div class="calib-step-label">保存标定文件</div>
                </div>
            </div>
            <div class="calib-wizard-body">
                <div class="calib-step-content active" id="calib-step-1-content">
                    <div class="calib-layout-s1">
                        <div class="calib-camera-view" id="calib-camera-view">
                            <div class="calib-camera-placeholder">
                                <i class="fas fa-camera" style="font-size: 32px; margin-bottom: 8px;"></i>
                                <span id="calib-camera-placeholder-primary">请先在设置页相机管理中选择一台相机</span>
                                <span id="calib-camera-placeholder-secondary">点击“刷新预览”获取一帧图像后，再在画面上选点</span>
                            </div>
                            <img id="calib-camera-img" class="calib-camera-img" style="display:none;" />
                            <div id="calib-marker" class="calib-point-marker" style="display:none;"></div>
                        </div>
                        <div class="calib-data-panel">
                            <div class="calib-input-group">
                                <div style="font-size: 13px; color: var(--text-secondary); margin-bottom: 4px; font-weight: 500;">当前点位录入 (要求最少 2 个点)</div>
                                <div class="calib-input-row">
                                    <label>像素 X:</label>
                                    <input type="number" id="calib-px" step="0.1" placeholder="点击图像获取" readonly>
                                </div>
                                <div class="calib-input-row">
                                    <label>像素 Y:</label>
                                    <input type="number" id="calib-py" step="0.1" placeholder="点击图像获取" readonly>
                                </div>
                                <div class="calib-input-row" style="margin-top: 8px;">
                                    <label>物理 X:</label>
                                    <input type="number" id="calib-hx" step="0.001" placeholder="示教器 X 坐标 (mm)">
                                </div>
                                <div class="calib-input-row">
                                    <label>物理 Y:</label>
                                    <input type="number" id="calib-hy" step="0.001" placeholder="示教器 Y 坐标 (mm)">
                                </div>
                                <button class="calib-btn-add" id="calib-btn-add" type="button" disabled>添加标定点</button>
                                <button class="calib-btn calib-btn-prev" id="calib-btn-refresh-preview" type="button" style="margin-top: 8px;">刷新预览</button>
                            </div>

                            <div class="calib-table-container">
                                <table class="calib-table">
                                    <thead>
                                        <tr>
                                            <th width="40px">#</th>
                                            <th>像素 (X, Y)</th>
                                            <th>物理 (X, Y)</th>
                                            <th width="40px">操作</th>
                                        </tr>
                                    </thead>
                                    <tbody id="calib-table-body"></tbody>
                                </table>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="calib-step-content" id="calib-step-2-content">
                    <div class="calib-layout-s2">
                        <div style="text-align: center;">
                            <h3 style="margin-bottom: 8px; font-size: 18px; color: var(--text-primary);">开始解算标定矩阵</h3>
                            <p style="color: var(--text-secondary); font-size: 14px;" id="calib-s2-desc">已采集 0 个有效点位，将使用最小二乘法进行线性回归解算。</p>
                        </div>

                        <button class="calib-solve-btn" id="calib-btn-solve" type="button">
                            <span style="font-size: 20px;">▶</span> 执行计算
                        </button>

                        <div class="calib-solve-result" id="calib-solve-result">
                            <h4 style="margin: 0 0 16px 0; color: var(--text-primary); display: flex; align-items: center; justify-content: space-between;">
                                <span>解算成功</span>
                                <span id="calib-status-badge" style="font-size: 12px; padding: 4px 8px; border-radius: 4px; background: #e0e7ff; color: #4338ca;">数据更新</span>
                            </h4>

                            <div class="calib-result-grid">
                                <div class="calib-metric">
                                    <div class="calib-metric-title">重投影根均方误差 (RMSE)</div>
                                    <div class="calib-metric-value" id="calib-res-rmse">0.000 <span class="calib-metric-unit">mm</span></div>
                                </div>
                                <div class="calib-metric">
                                    <div class="calib-metric-title">平均像素物理尺寸 (Scale)</div>
                                    <div class="calib-metric-value" id="calib-res-scale">0.000000 <span class="calib-metric-unit">mm/px</span></div>
                                </div>
                                <div class="calib-metric">
                                    <div class="calib-metric-title">坐标原点 X (Origin X)</div>
                                    <div class="calib-metric-value" id="calib-res-ox">0.000 <span class="calib-metric-unit">mm</span></div>
                                </div>
                                <div class="calib-metric">
                                    <div class="calib-metric-title">坐标原点 Y (Origin Y)</div>
                                    <div class="calib-metric-value" id="calib-res-oy">0.000 <span class="calib-metric-unit">mm</span></div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="calib-step-content" id="calib-step-3-content">
                    <div class="calib-layout-s3">
                        <div class="calib-save-icon">✓</div>
                        <h3 style="margin: 0; font-size: 20px; color: var(--text-primary);">标定数据已就绪</h3>
                        <p style="color: var(--text-secondary); font-size: 14px; margin: 0 0 10px 0;">
                            保存后，您可以在流程中配置 CoordinateTransform(HandEye) 算子关联此文件。
                        </p>

                        <div class="calib-save-input-group">
                            <label>保存标定文件:</label>
                            <input type="text" id="calib-filename" value="hand_eye_calib.json">
                        </div>
                    </div>
                </div>
            </div>
            <div class="calib-wizard-footer">
                <button class="calib-btn calib-btn-prev" id="calib-btn-prev" type="button" style="visibility: hidden;">上一步</button>
                <button class="calib-btn calib-btn-next" id="calib-btn-next" type="button" disabled>下一步</button>
            </div>
        `;

        this.overlay.appendChild(modal);

        this.els = {
            modal,
            step1: this.overlay.querySelector('#calib-step-1-content'),
            step2: this.overlay.querySelector('#calib-step-2-content'),
            step3: this.overlay.querySelector('#calib-step-3-content'),
            indic1: this.overlay.querySelector('#calib-step-1-indic'),
            indic2: this.overlay.querySelector('#calib-step-2-indic'),
            indic3: this.overlay.querySelector('#calib-step-3-indic'),
            btnPrev: this.overlay.querySelector('#calib-btn-prev'),
            btnNext: this.overlay.querySelector('#calib-btn-next'),
            btnClose: this.overlay.querySelector('#calib-btn-close'),
            btnRefreshPreview: this.overlay.querySelector('#calib-btn-refresh-preview'),
            camImg: this.overlay.querySelector('#calib-camera-img'),
            camArea: this.overlay.querySelector('#calib-camera-view'),
            marker: this.overlay.querySelector('#calib-marker'),
            placeholder: this.overlay.querySelector('.calib-camera-placeholder'),
            placeholderPrimary: this.overlay.querySelector('#calib-camera-placeholder-primary'),
            placeholderSecondary: this.overlay.querySelector('#calib-camera-placeholder-secondary'),
            inpPx: this.overlay.querySelector('#calib-px'),
            inpPy: this.overlay.querySelector('#calib-py'),
            inpHx: this.overlay.querySelector('#calib-hx'),
            inpHy: this.overlay.querySelector('#calib-hy'),
            btnAdd: this.overlay.querySelector('#calib-btn-add'),
            tbody: this.overlay.querySelector('#calib-table-body'),
            desc2: this.overlay.querySelector('#calib-s2-desc'),
            btnSolve: this.overlay.querySelector('#calib-btn-solve'),
            resPanel: this.overlay.querySelector('#calib-solve-result'),
            valRmse: this.overlay.querySelector('#calib-res-rmse'),
            valScale: this.overlay.querySelector('#calib-res-scale'),
            valOx: this.overlay.querySelector('#calib-res-ox'),
            valOy: this.overlay.querySelector('#calib-res-oy'),
            badge: this.overlay.querySelector('#calib-status-badge'),
            inpFilename: this.overlay.querySelector('#calib-filename')
        };
    }

    attachEvents() {
        this.els.btnClose.addEventListener('click', () => this.hide());
        this.overlay.addEventListener('click', (event) => {
            if (event.target === this.overlay) {
                this.hide();
            }
        });

        this.els.btnNext.addEventListener('click', () => {
            if (this.currentStep === 3) {
                this.saveCalibration();
            } else {
                this.goToStep(this.currentStep + 1);
            }
        });

        this.els.btnPrev.addEventListener('click', () => {
            this.goToStep(this.currentStep - 1);
        });

        this.els.btnRefreshPreview?.addEventListener('click', () => {
            this.refreshPreviewFrame();
        });

        this.els.camImg.addEventListener('click', (event) => {
            const rect = this.els.camImg.getBoundingClientRect();
            const normalizedX = (event.clientX - rect.left) / rect.width;
            const normalizedY = (event.clientY - rect.top) / rect.height;
            const actualPx = normalizedX * this.els.camImg.naturalWidth;
            const actualPy = normalizedY * this.els.camImg.naturalHeight;

            this.els.inpPx.value = actualPx.toFixed(1);
            this.els.inpPy.value = actualPy.toFixed(1);

            this.els.marker.style.display = 'block';
            this.els.marker.style.left = normalizedX * 100 + '%';
            this.els.marker.style.top = normalizedY * 100 + '%';

            this.checkAddButtonState();
            this.els.inpHx.focus();
        });

        [this.els.inpPx, this.els.inpPy, this.els.inpHx, this.els.inpHy].forEach((el) => {
            el.addEventListener('input', () => this.checkAddButtonState());
        });

        this.els.btnAdd.addEventListener('click', () => {
            const point = {
                pixelX: parseFloat(this.els.inpPx.value),
                pixelY: parseFloat(this.els.inpPy.value),
                physicalX: parseFloat(this.els.inpHx.value),
                physicalY: parseFloat(this.els.inpHy.value)
            };

            this.points.push(point);
            this.renderTable();
            this.clearCurrentPointInputs();
        });

        this.els.btnSolve.addEventListener('click', () => {
            this.solveCalibration();
        });
    }

    checkAddButtonState() {
        const hasValidInputs = this.els.inpPx.value !== ''
            && this.els.inpPy.value !== ''
            && this.els.inpHx.value !== ''
            && this.els.inpHy.value !== '';
        this.els.btnAdd.disabled = !hasValidInputs;
    }

    clearCurrentPointInputs() {
        this.els.inpPx.value = '';
        this.els.inpPy.value = '';
        this.els.inpHx.value = '';
        this.els.inpHy.value = '';
        this.els.marker.style.display = 'none';
        this.checkAddButtonState();
    }

    renderTable() {
        this.els.tbody.innerHTML = '';
        this.points.forEach((point, index) => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${index + 1}</td>
                <td>(${point.pixelX}, ${point.pixelY})</td>
                <td>(${point.physicalX}, ${point.physicalY})</td>
                <td><button class="calib-btn-del" type="button" data-index="${index}">×</button></td>
            `;
            this.els.tbody.appendChild(row);
        });

        this.els.tbody.querySelectorAll('.calib-btn-del').forEach((button) => {
            button.addEventListener('click', (event) => {
                const index = parseInt(event.currentTarget.dataset.index, 10);
                this.points.splice(index, 1);
                this.renderTable();
            });
        });

        this.els.btnNext.disabled = this.points.length < 2;
    }

    goToStep(step) {
        if (step < 1 || step > 3) return;
        this.currentStep = step;

        this.els.step1.classList.toggle('active', step === 1);
        this.els.step2.classList.toggle('active', step === 2);
        this.els.step3.classList.toggle('active', step === 3);

        this.els.indic1.className = `calib-step ${step >= 1 ? 'active ' : ''}${step > 1 ? 'completed' : ''}`.trim();
        this.els.indic2.className = `calib-step ${step >= 2 ? 'active ' : ''}${step > 2 ? 'completed' : ''}`.trim();
        this.els.indic3.className = `calib-step ${step >= 3 ? 'active' : ''}`.trim();

        this.els.btnPrev.style.visibility = step === 1 ? 'hidden' : 'visible';

        if (step === 1) {
            this.els.btnNext.textContent = '下一步';
            this.els.btnNext.disabled = this.points.length < 2;
        } else if (step === 2) {
            this.els.btnNext.textContent = '下一步';
            this.els.btnNext.disabled = !this.solveResult?.success;
            this.els.desc2.textContent = `已采集 ${this.points.length} 个点位，点击执行进行最小二乘法解算。`;
        } else {
            this.els.btnNext.textContent = '保存并完成';
            this.els.btnNext.disabled = false;
        }
    }

    show() {
        if (!this.overlay.isConnected) {
            document.body.appendChild(this.overlay);
        }

        this.overlay.classList.add('visible');
        this.goToStep(1);

        if (this.cameraManager) {
            this._boundRenderFrame = this.renderFrame.bind(this);
            this.cameraManager.addEventListener('frame', this._boundRenderFrame);
        }

        if (!this._boundKeydown) {
            this._boundKeydown = (event) => {
                if (event.key === 'Escape') {
                    this.hide();
                }
            };
        }
        window.addEventListener('keydown', this._boundKeydown);
        window.chrome?.webview?.addEventListener('message', this.onWebMessageReceived);

        this.refreshPreviewFrame();
    }

    hide() {
        this.overlay.classList.remove('visible');

        if (this.cameraManager && this._boundRenderFrame) {
            this.cameraManager.removeEventListener('frame', this._boundRenderFrame);
            this._boundRenderFrame = null;
        }

        if (this._boundKeydown) {
            window.removeEventListener('keydown', this._boundKeydown);
        }

        window.chrome?.webview?.removeEventListener('message', this.onWebMessageReceived);
        this.destroyPreviewUrl();
        this.overlay.remove();
    }

    destroyPreviewUrl() {
        if (this._cameraPreviewUrl) {
            URL.revokeObjectURL(this._cameraPreviewUrl);
            this._cameraPreviewUrl = null;
        }
    }

    togglePlaceholder(visible, primaryText = null, secondaryText = null) {
        this.els.placeholder.style.display = visible ? 'flex' : 'none';
        this.els.camImg.style.display = visible ? 'none' : 'block';
        if (primaryText) {
            this.els.placeholderPrimary.textContent = primaryText;
        }
        if (secondaryText) {
            this.els.placeholderSecondary.textContent = secondaryText;
        }
    }

    async refreshPreviewFrame() {
        if (!this.captureFrame) {
            this.togglePlaceholder(
                true,
                '当前环境未接入相机预览能力',
                '请返回设置页相机管理确认相机预览是否可用'
            );
            return;
        }

        const cameraBindingId = this.getCameraBindingId?.() || null;
        if (!cameraBindingId) {
            this.togglePlaceholder(
                true,
                '请先在设置页相机管理中选择一台相机',
                '然后点击“刷新预览”获取一帧图像'
            );
            return;
        }

        try {
            this.els.btnRefreshPreview.disabled = true;
            this.els.btnRefreshPreview.textContent = '预览加载中...';
            const preview = await this.captureFrame(cameraBindingId);

            this.destroyPreviewUrl();
            this._cameraPreviewUrl = preview.imageUrl;
            this.els.camImg.src = preview.imageUrl;
            this.togglePlaceholder(false);
        } catch (error) {
            this.togglePlaceholder(
                true,
                '相机预览加载失败',
                error.message || '请检查相机连接、曝光参数和软触发链路'
            );
        } finally {
            this.els.btnRefreshPreview.disabled = false;
            this.els.btnRefreshPreview.textContent = '刷新预览';
        }
    }

    renderFrame(event) {
        if (!event.detail?.data) return;
        this.togglePlaceholder(false);
        this.els.camImg.src = `data:image/jpeg;base64,${event.detail.data}`;
    }

    solveCalibration() {
        this.els.btnSolve.innerHTML = '<span style="font-size: 20px;">◌</span> 解算中...';
        this.els.btnSolve.disabled = true;

        if (window.chrome?.webview) {
            window.chrome.webview.postMessage({
                messageType: 'handeye:solve',
                payload: this.points
            });
            return;
        }

        setTimeout(() => {
            this.handleSolveResult({
                success: true,
                rmse: 0.043,
                scaleX: 0.051,
                scaleY: 0.051,
                originX: Math.random() * 10,
                originY: -Math.random() * 10
            });
        }, 500);
    }

    handleSolveResult(result) {
        this.els.btnSolve.innerHTML = '<span style="font-size: 20px;">▶</span> 重新执行计算';
        this.els.btnSolve.disabled = false;

        if (!result.success) {
            alert(`标定解算失败: ${result.message || '数据异常或共线'}`);
            return;
        }

        this.solveResult = result;
        this.els.resPanel.classList.add('visible');

        this.els.valRmse.innerHTML = `${parseFloat(result.rmse).toFixed(3)} <span class="calib-metric-unit">mm</span>`;
        if (result.rmse < 1.0) {
            this.els.valRmse.parentElement.classList.remove('calib-rmse-warn');
            this.els.valRmse.parentElement.classList.add('calib-rmse-ok');
        } else {
            this.els.valRmse.parentElement.classList.remove('calib-rmse-ok');
            this.els.valRmse.parentElement.classList.add('calib-rmse-warn');
        }

        const scaleAvg = ((Math.abs(result.scaleX) + Math.abs(result.scaleY)) / 2).toFixed(6);
        this.els.valScale.innerHTML = `${scaleAvg} <span class="calib-metric-unit">mm/px</span>`;
        this.els.valOx.innerHTML = `${parseFloat(result.originX).toFixed(3)} <span class="calib-metric-unit">mm</span>`;
        this.els.valOy.innerHTML = `${parseFloat(result.originY).toFixed(3)} <span class="calib-metric-unit">mm</span>`;

        this.els.btnNext.disabled = false;
    }

    saveCalibration() {
        const fileName = this.els.inpFilename.value.trim() || 'hand_eye_calib.json';
        this.els.btnNext.disabled = true;
        this.els.btnNext.textContent = '保存中...';

        if (window.chrome?.webview) {
            window.chrome.webview.postMessage({
                messageType: 'handeye:save',
                payload: {
                    fileName,
                    result: this.solveResult
                }
            });
            return;
        }

        setTimeout(() => this.handleSaveResult({ success: true }), 500);
    }

    handleSaveResult(result) {
        this.els.btnNext.disabled = false;
        this.els.btnNext.textContent = '保存并完成';

        if (!result.success) {
            alert(`保存失败: ${result.message || '未知错误'}`);
            return;
        }

        const toast = document.createElement('div');
        toast.style.cssText = 'position: fixed; top: 20px; right: 20px; background: #10b981; color: white; padding: 12px 24px; border-radius: 8px; font-weight: 500; font-size: 14px; box-shadow: 0 4px 12px rgba(16,185,129,0.3); z-index: 10000;';
        toast.textContent = '✓ 标定文件保存成功';
        document.body.appendChild(toast);

        setTimeout(() => {
            toast.remove();
            this.hide();
        }, 1200);
    }

    onWebMessageReceived(event) {
        try {
            const data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
            if (data.messageType === 'handeye:solve:result') {
                this.handleSolveResult(data.payload);
            } else if (data.messageType === 'handeye:save:result') {
                this.handleSaveResult(data.payload);
            }
        } catch (error) {
            console.error('[HandEye] Message parse error', error);
        }
    }
}
