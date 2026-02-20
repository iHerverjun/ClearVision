/**
 * handEyeCalibWizard.js
 * 手眼标定三步向导组件
 * 
 * Step 1: 采集标定点
 * Step 2: 验证与求解
 * Step 3: 保存标定文件
 */

export class HandEyeCalibWizard {
    constructor(cameraManager) {
        this.cameraManager = cameraManager;
        this.currentStep = 1;
        this.points = [];
        this.solveResult = null;
        
        // 绑定消息接收方法
        this.onWebMessageReceived = this.onWebMessageReceived.bind(this);
        
        this.createUI();
        this.attachEvents();
    }

    createUI() {
        // 创建遮罩和主容器
        this.overlay = document.createElement('div');
        this.overlay.className = 'calib-wizard-overlay';
        
        const modal = document.createElement('div');
        modal.className = 'calib-wizard-modal';
        
        // --- Header ---
        const header = document.createElement('div');
        header.className = 'calib-wizard-header';
        header.innerHTML = `
            <div class="calib-wizard-title">
                <span>🔧</span> 手眼标定向导 (N点平移缩放标定)
            </div>
            <button class="calib-wizard-close" id="calib-btn-close">×</button>
        `;
        
        // --- Stepper ---
        const stepper = document.createElement('div');
        stepper.className = 'calib-stepper';
        stepper.innerHTML = `
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
        `;
        
        // --- Body ---
        const body = document.createElement('div');
        body.className = 'calib-wizard-body';
        
        // Step 1 Content
        const step1Content = document.createElement('div');
        step1Content.className = 'calib-step-content active';
        step1Content.id = 'calib-step-1-content';
        step1Content.innerHTML = `
            <div class="calib-layout-s1">
                <div class="calib-camera-view" id="calib-camera-view">
                    <div class="calib-camera-placeholder">
                        <i class="fas fa-camera" style="font-size: 32px; margin-bottom: 8px;"></i>
                        <span>请在设置面板中开启相机预览</span>
                        <span>点击画面特征点以提取像素坐标</span>
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
                        <button class="calib-btn-add" id="calib-btn-add" disabled>添加标定点</button>
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
                            <tbody id="calib-table-body">
                                <!-- Data rows -->
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        `;
        
        // Step 2 Content
        const step2Content = document.createElement('div');
        step2Content.className = 'calib-step-content';
        step2Content.id = 'calib-step-2-content';
        step2Content.innerHTML = `
            <div class="calib-layout-s2">
                <div style="text-align: center;">
                    <h3 style="margin-bottom: 8px; font-size: 18px; color: var(--text-primary);">开始解算标定矩阵</h3>
                    <p style="color: var(--text-secondary); font-size: 14px;" id="calib-s2-desc">已采集 0 个有效点位，将使用最小二乘法进行线性回归解算。</p>
                </div>
                
                <button class="calib-solve-btn" id="calib-btn-solve">
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
        `;
        
        // Step 3 Content
        const step3Content = document.createElement('div');
        step3Content.className = 'calib-step-content';
        step3Content.id = 'calib-step-3-content';
        step3Content.innerHTML = `
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
        `;
        
        body.appendChild(step1Content);
        body.appendChild(step2Content);
        body.appendChild(step3Content);
        
        // --- Footer ---
        const footer = document.createElement('div');
        footer.className = 'calib-wizard-footer';
        footer.innerHTML = `
            <button class="calib-btn calib-btn-prev" id="calib-btn-prev" style="visibility: hidden;">上一步</button>
            <button class="calib-btn calib-btn-next" id="calib-btn-next" disabled>下一步</button>
        `;
        
        modal.appendChild(header);
        modal.appendChild(stepper);
        modal.appendChild(body);
        modal.appendChild(footer);
        
        this.overlay.appendChild(modal);
        document.body.appendChild(this.overlay);
        
        // Caching DOM elements
        this.els = {
            step1: document.getElementById('calib-step-1-content'),
            step2: document.getElementById('calib-step-2-content'),
            step3: document.getElementById('calib-step-3-content'),
            indic1: document.getElementById('calib-step-1-indic'),
            indic2: document.getElementById('calib-step-2-indic'),
            indic3: document.getElementById('calib-step-3-indic'),
            btnPrev: document.getElementById('calib-btn-prev'),
            btnNext: document.getElementById('calib-btn-next'),
            btnClose: document.getElementById('calib-btn-close'),
            
            // Step 1
            camImg: document.getElementById('calib-camera-img'),
            camArea: document.getElementById('calib-camera-view'),
            marker: document.getElementById('calib-marker'),
            inpPx: document.getElementById('calib-px'),
            inpPy: document.getElementById('calib-py'),
            inpHx: document.getElementById('calib-hx'),
            inpHy: document.getElementById('calib-hy'),
            btnAdd: document.getElementById('calib-btn-add'),
            tbody: document.getElementById('calib-table-body'),
            
            // Step 2
            desc2: document.getElementById('calib-s2-desc'),
            btnSolve: document.getElementById('calib-btn-solve'),
            resPanel: document.getElementById('calib-solve-result'),
            valRmse: document.getElementById('calib-res-rmse'),
            valScale: document.getElementById('calib-res-scale'),
            valOx: document.getElementById('calib-res-ox'),
            valOy: document.getElementById('calib-res-oy'),
            badge: document.getElementById('calib-status-badge'),
            
            // Step 3
            inpFilename: document.getElementById('calib-filename')
        };
    }

    attachEvents() {
        this.els.btnClose.addEventListener('click', () => this.hide());
        
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

        // Step 1: Camera click
        this.els.camImg.addEventListener('click', (e) => {
            const rect = this.els.camImg.getBoundingClientRect();
            // 计算在原始图片的像素比例
            const pX = (e.clientX - rect.left) / rect.width;
            const pY = (e.clientY - rect.top) / rect.height;
            
            // 需要获取真实图像尺寸（如果后端暴露），目前通过图像 naturalWidth 来折算
            const actualPx = pX * this.els.camImg.naturalWidth;
            const actualPy = pY * this.els.camImg.naturalHeight;
            
            // Update input fields
            this.els.inpPx.value = actualPx.toFixed(1);
            this.els.inpPy.value = actualPy.toFixed(1);
            
            // UI Marker
            this.els.marker.style.display = 'block';
            this.els.marker.style.left = pX * 100 + '%';
            this.els.marker.style.top = pY * 100 + '%';
            
            this.checkAddButtonState();
            this.els.inpHx.focus(); // 聚焦准备输入物理坐标
        });

        // inputs change
        [this.els.inpPx, this.els.inpPy, this.els.inpHx, this.els.inpHy].forEach(el => {
            el.addEventListener('input', () => this.checkAddButtonState());
        });

        // Add Point
        this.els.btnAdd.addEventListener('click', () => {
            const pt = {
                pixelX: parseFloat(this.els.inpPx.value),
                pixelY: parseFloat(this.els.inpPy.value),
                physicalX: parseFloat(this.els.inpHx.value),
                physicalY: parseFloat(this.els.inpHy.value)
            };
            
            this.points.push(pt);
            this.renderTable();
            
            // Clear inputs
            this.els.inpPx.value = '';
            this.els.inpPy.value = '';
            this.els.inpHx.value = '';
            this.els.inpHy.value = '';
            this.els.marker.style.display = 'none';
            this.checkAddButtonState();
        });

        // Step 2: Solve
        this.els.btnSolve.addEventListener('click', () => {
            this.solveCalibration();
        });
    }

    checkAddButtonState() {
        const hasValidInputs = 
            this.els.inpPx.value !== '' && 
            this.els.inpPy.value !== '' && 
            this.els.inpHx.value !== '' && 
            this.els.inpHy.value !== '';
            
        this.els.btnAdd.disabled = !hasValidInputs;
    }

    renderTable() {
        this.els.tbody.innerHTML = '';
        this.points.forEach((pt, idx) => {
            const tr = document.createElement('tr');
            tr.innerHTML = '<td>' + (idx + 1) + '</td>' +
                           '<td>(' + pt.pixelX + ', ' + pt.pixelY + ')</td>' +
                           '<td>(' + pt.physicalX + ', ' + pt.physicalY + ')</td>' +
                           '<td><button class="calib-btn-del" data-index="' + idx + '">×</button></td>';
            this.els.tbody.appendChild(tr);
        });
        
        // Bind delete
        this.els.tbody.querySelectorAll('.calib-btn-del').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const i = parseInt(e.target.dataset.index);
                this.points.splice(i, 1);
                this.renderTable();
            });
        });
        
        // 至少 2 个点才能进入下一步
        this.els.btnNext.disabled = this.points.length < 2;
    }

    goToStep(step) {
        if (step < 1 || step > 3) return;
        this.currentStep = step;
        
        // Update content visibility
        this.els.step1.classList.toggle('active', step === 1);
        this.els.step2.classList.toggle('active', step === 2);
        this.els.step3.classList.toggle('active', step === 3);
        
        // Update indicators
        this.els.indic1.className = 'calib-step ' + (step >= 1 ? 'active ' : '') + (step > 1 ? 'completed' : '');
        this.els.indic2.className = 'calib-step ' + (step >= 2 ? 'active ' : '') + (step > 2 ? 'completed' : '');
        this.els.indic3.className = 'calib-step ' + (step >= 3 ? 'active' : '');
        
        // Update footer buttons
        this.els.btnPrev.style.visibility = step === 1 ? 'hidden' : 'visible';
        
        if (step === 1) {
            this.els.btnNext.textContent = '下一步';
            this.els.btnNext.disabled = this.points.length < 2;
        } else if (step === 2) {
            this.els.btnNext.textContent = '下一步';
            this.els.btnNext.disabled = !this.solveResult?.success;
            this.els.desc2.textContent = '已采集 ' + this.points.length + ' 个点位，点击执行进行最小二乘法解算。';
        } else if (step === 3) {
            this.els.btnNext.textContent = '保存并完成';
            this.els.btnNext.disabled = false;
        }
    }

    show() {
        this.overlay.classList.add('visible');
        this.goToStep(1);
        
        // 接管相机渲染事件
        if (this.cameraManager) {
            this._boundRenderFrame = this.renderFrame.bind(this);
            this.cameraManager.addEventListener('frame', this._boundRenderFrame);
        }
        
        // 绑定系统消息
        window.chrome?.webview?.addEventListener('message', this.onWebMessageReceived);
    }

    hide() {
        this.overlay.classList.remove('visible');
        
        if (this.cameraManager && this._boundRenderFrame) {
            this.cameraManager.removeEventListener('frame', this._boundRenderFrame);
        }
        window.chrome?.webview?.removeEventListener('message', this.onWebMessageReceived);
    }

    renderFrame(event) {
        if (!event.detail || !event.detail.data) return;
        this.els.camImg.style.display = 'block';
        document.querySelector('.calib-camera-placeholder').style.display = 'none';
        this.els.camImg.src = 'data:image/jpeg;base64,' + event.detail.data;
    }

    solveCalibration() {
        this.els.btnSolve.innerHTML = '<span class="status-indicator processing"></span> 解算中...';
        this.els.btnSolve.disabled = true;
        
        if (window.chrome?.webview) {
            window.chrome.webview.postMessage({
                messageType: 'handeye:solve',
                payload: this.points
            });
        } else {
            // Mock for dev
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
    }

    handleSolveResult(res) {
        this.els.btnSolve.innerHTML = '<span style="font-size: 20px;">▶</span> 重新执行计算';
        this.els.btnSolve.disabled = false;
        
        if (!res.success) {
            alert('标定解算失败: ' + (res.message || '数据异常共线'));
            return;
        }
        
        this.solveResult = res;
        this.els.resPanel.classList.add('visible');
        
        // 格式化展示
        const rmseVal = parseFloat(res.rmse).toFixed(3);
        this.els.valRmse.innerHTML = rmseVal + ' <span class="calib-metric-unit">mm</span>';
        
        // RMSE 小于 1mm 时显示绿色，否则橙色警告
        if (res.rmse < 1.0) {
            this.els.valRmse.parentElement.classList.remove('calib-rmse-warn');
            this.els.valRmse.parentElement.classList.add('calib-rmse-ok');
        } else {
            this.els.valRmse.parentElement.classList.remove('calib-rmse-ok');
            this.els.valRmse.parentElement.classList.add('calib-rmse-warn');
        }
        
        const scaleAvg = ((Math.abs(res.scaleX) + Math.abs(res.scaleY)) / 2).toFixed(6);
        this.els.valScale.innerHTML = scaleAvg + ' <span class="calib-metric-unit">mm/px</span>';
        
        this.els.valOx.innerHTML = parseFloat(res.originX).toFixed(3) + ' <span class="calib-metric-unit">mm</span>';
        this.els.valOy.innerHTML = parseFloat(res.originY).toFixed(3) + ' <span class="calib-metric-unit">mm</span>';
        
        this.els.btnNext.disabled = false;
    }

    saveCalibration() {
        const fn = this.els.inpFilename.value.trim() || 'hand_eye_calib.json';
        this.els.btnNext.disabled = true;
        this.els.btnNext.textContent = '保存中...';
        
        if (window.chrome?.webview) {
            window.chrome.webview.postMessage({
                messageType: 'handeye:save',
                payload: {
                    fileName: fn,
                    result: this.solveResult
                }
            });
        } else {
            setTimeout(() => this.handleSaveResult({ success: true }), 500);
        }
    }

    handleSaveResult(res) {
        this.els.btnNext.disabled = false;
        this.els.btnNext.textContent = '保存并完成';
        
        if (res.success) {
            // Toast or alert
            const alertHtml = '' +
                '<div style="position: fixed; top: 20px; right: 20px; background: #10b981; color: white; padding: 12px 24px; border-radius: 8px; font-weight: 500; font-size: 14px; box-shadow: 0 4px 12px rgba(16,185,129,0.3); z-index: 10000; animation: fadeIn 0.3s ease;">' +
                    '✅ 标定文件保存成功！' +
                '</div>';
            document.body.insertAdjacentHTML('beforeend', alertHtml);
            setTimeout(() => {
                document.body.lastElementChild.remove();
                this.hide();
            }, 2000);
        } else {
            alert('保存失败: ' + (res.message || '未知错误'));
        }
    }

    onWebMessageReceived(event) {
        try {
            const data = (typeof event.data === 'string') ? JSON.parse(event.data) : event.data;
            if (data.messageType === 'handeye:solve:result') {
                this.handleSolveResult(data.payload);
            } else if (data.messageType === 'handeye:save:result') {
                this.handleSaveResult(data.payload);
            }
        } catch (err) {
            console.error('[HandEye] Message parse error', err);
        }
    }
}
