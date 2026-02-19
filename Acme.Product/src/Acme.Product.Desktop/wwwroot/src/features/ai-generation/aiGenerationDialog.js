import webMessageBridge from '../../core/messaging/webMessageBridge.js';

/**
 * AI 工作流生成对话框
 * 提供自然语言输入，调用后端 AI 生成接口，并将结果渲染到画布
 */
export class AiGenerationDialog {
    constructor(canvas) {
        this.canvas = canvas;
        this.isGenerating = false;
        this._init();
    }

    _init() {
        this._injectStyles();
        this._createDialogDom();
        this._bindEvents();
        this._setupMessageListener();
    }

    /**
     * 打开对话框
     */
    open() {
        document.getElementById("ai-gen-overlay").style.display = "flex";
        document.getElementById("ai-gen-input").focus();
    }

    /**
     * 关闭对话框
     */
    close() {
        if (this.isGenerating) return; // 生成中不允许关闭
        document.getElementById("ai-gen-overlay").style.display = "none";
        this._resetState();
    }

    _createDialogDom() {
        const overlay = document.createElement("div");
        overlay.id = "ai-gen-overlay";
        overlay.innerHTML = `
            <div class="ai-gen-dialog">
                <div class="ai-gen-header">
                    <span class="ai-gen-title">✨ AI 一键生成工程</span>
                    <button class="ai-gen-close" id="ai-gen-close-btn">×</button>
                </div>

                <div class="ai-gen-body">
                    <div class="ai-gen-tip">
                        用自然语言描述你的检测需求，AI 将自动选择算子并连线。
                    </div>

                    <!-- 示例场景快捷选择 -->
                    <div class="ai-gen-examples">
                        <span class="ai-gen-examples-label">快速示例：</span>
                        <button class="ai-gen-example-btn" data-text="用相机拍照检测产品表面缺陷，统计缺陷数量">缺陷检测</button>
                        <button class="ai-gen-example-btn" data-text="扫描产品上的二维码，识别内容后通过Modbus发送给PLC">条码读取</button>
                        <button class="ai-gen-example-btn" data-text="测量两个圆孔之间的距离，需要先进行相机标定">孔距测量</button>
                        <button class="ai-gen-example-btn" data-text="用深度学习检测缺陷，同时用传统算法验证，两者都通过才算合格">双模态检测</button>
                    </div>

                    <!-- 输入框 -->
                    <textarea
                        id="ai-gen-input"
                        class="ai-gen-textarea"
                        placeholder="例如：用相机采集图像，检测产品表面划痕，将检测结果保存到数据库..."
                        rows="4"
                    ></textarea>

                    <!-- 进度区域（生成中显示） -->
                    <div class="ai-gen-progress" id="ai-gen-progress" style="display:none">
                        <div class="ai-gen-spinner"></div>
                        <span id="ai-gen-progress-text">正在连接 AI...</span>
                    </div>

                    <!-- AI 说明（生成成功后显示） -->
                    <div class="ai-gen-explanation" id="ai-gen-explanation" style="display:none">
                        <div class="ai-gen-explanation-label">💡 AI 说明</div>
                        <div id="ai-gen-explanation-text" class="ai-gen-explanation-content"></div>
                    </div>

                    <!-- AI 思维链（可折叠） -->
                    <div class="ai-gen-reasoning" id="ai-gen-reasoning" style="display:none">
                        <div class="ai-gen-reasoning-label" id="ai-gen-reasoning-toggle" style="cursor:pointer;user-select:none;">
                            🧠 AI 推理过程 <span style="font-size:11px;color:#64748b;">▼ 点击展开</span>
                        </div>
                        <div id="ai-gen-reasoning-text" class="ai-gen-reasoning-content" style="display:none"></div>
                    </div>

                    <!-- 错误信息 -->
                    <div class="ai-gen-error" id="ai-gen-error" style="display:none">
                        <span id="ai-gen-error-text"></span>
                    </div>

                    <!-- 参数确认提示 -->
                    <div class="ai-gen-review-hint" id="ai-gen-review-hint" style="display:none">
                        ⚠️ 部分参数需要你手动确认（画布中已用标志提示）
                    </div>
                </div>

                <div class="ai-gen-footer">
                    <button class="ai-gen-btn-cancel" id="ai-gen-cancel-btn">取消</button>
                    <button class="ai-gen-btn-generate" id="ai-gen-generate-btn">
                        ✨ 生成工程
                    </button>
                </div>
            </div>
        `;
        document.body.appendChild(overlay);
    }

    _bindEvents() {
        // 关闭按钮
        document
            .getElementById("ai-gen-close-btn")
            .addEventListener("click", () => this.close());
        document
            .getElementById("ai-gen-cancel-btn")
            .addEventListener("click", () => this.close());

        // 点击遮罩关闭
        document
            .getElementById("ai-gen-overlay")
            .addEventListener("click", (e) => {
                if (e.target.id === "ai-gen-overlay") this.close();
            });

        // 生成按钮
        document
            .getElementById("ai-gen-generate-btn")
            .addEventListener("click", () => this._handleGenerate());

        // 快捷示例按钮
        document.querySelectorAll(".ai-gen-example-btn").forEach((btn) => {
            btn.addEventListener("click", () => {
                document.getElementById("ai-gen-input").value =
                    btn.dataset.text;
            });
        });

        // 思维链折叠切换
        document.getElementById("ai-gen-reasoning-toggle")?.addEventListener("click", () => {
            const content = document.getElementById("ai-gen-reasoning-text");
            if (content) {
                content.style.display = content.style.display === 'none' ? 'block' : 'none';
            }
        });

        // Ctrl+Enter 快捷生成
        document
            .getElementById("ai-gen-input")
            .addEventListener("keydown", (e) => {
                if (e.ctrlKey && e.key === "Enter") this._handleGenerate();
            });
    }

    _setupMessageListener() {
        // 使用 webMessageBridge 监听后端消息
        webMessageBridge.on("GenerateFlowResult", (message) => this._handleGenerationResult(message));
        webMessageBridge.on("GenerateFlowProgress", (message) => this._updateProgress(message.message));
    }

    async _handleGenerate() {
        const description = document
            .getElementById("ai-gen-input")
            .value.trim();
        if (!description) {
            this._showError("请输入检测需求描述");
            return;
        }

        if (this.isGenerating) return;

        this._setGeneratingState(true);

        try {
            // 发送消息给后端
            webMessageBridge.sendMessage("GenerateFlow", { 
                payload: { description } 
            });
        } catch (err) {
            this._setGeneratingState(false);
            this._showError("发送请求失败：" + err.message);
        }
    }

    _handleGenerationResult(message) {
        this._setGeneratingState(false);

        if (!message.success) {
            this._showError(message.errorMessage || "AI 生成失败，请重试");
            return;
        }

        // 显示 AI 说明
        if (message.aiExplanation) {
            document.getElementById("ai-gen-explanation-text").textContent =
                message.aiExplanation;
            document.getElementById("ai-gen-explanation").style.display =
                "block";
        }

        // 显示 AI 思维链
        if (message.reasoning && message.reasoning.trim()) {
            document.getElementById("ai-gen-reasoning-text").textContent =
                message.reasoning;
            document.getElementById("ai-gen-reasoning").style.display = "block";
            document.getElementById("ai-gen-reasoning-text").style.display = "none"; // 默认收起
        }

        // 渲染到画布
        try {
            this.canvas.deserialize(message.flow);

            // 高亮需要用户确认的参数
            if (
                message.parametersNeedingReview &&
                Object.keys(message.parametersNeedingReview).length > 0
            ) {
                this._highlightReviewParams(message.parametersNeedingReview);
                document.getElementById("ai-gen-review-hint").style.display =
                    "block";
            }

            // 延迟关闭，让用户看到说明
            setTimeout(() => this.close(), 3000);
        } catch (err) {
            this._showError("工作流渲染失败：" + err.message);
            console.error("[AiGeneration] 渲染失败", err, message.flow);
        }
    }

    /**
     * 高亮需要用户确认参数的算子节点
     * @param {Object} paramsNeedingReview - { operatorId: ['param1', 'param2'] }
     */
    _highlightReviewParams(paramsNeedingReview) {
        // 通知画布高亮特定节点
        if (this.canvas && typeof this.canvas.highlightNodes === "function") {
            const nodeIds = Object.keys(paramsNeedingReview);
            this.canvas.highlightNodes(nodeIds, "review-needed");
        }
    }

    _setGeneratingState(generating) {
        this.isGenerating = generating;
        const btn = document.getElementById("ai-gen-generate-btn");
        const progress = document.getElementById("ai-gen-progress");
        const input = document.getElementById("ai-gen-input");

        btn.disabled = generating;
        btn.textContent = generating ? "生成中..." : "✨ 生成工程";
        progress.style.display = generating ? "flex" : "none";
        input.disabled = generating;

        // 清除之前的错误和说明
        if (generating) {
            document.getElementById("ai-gen-error").style.display = "none";
            document.getElementById("ai-gen-explanation").style.display =
                "none";
            document.getElementById("ai-gen-review-hint").style.display =
                "none";
        }
    }

    _updateProgress(message) {
        document.getElementById("ai-gen-progress-text").textContent = message;
    }

    _showError(message) {
        const el = document.getElementById("ai-gen-error");
        document.getElementById("ai-gen-error-text").textContent = message;
        el.style.display = "block";
    }

    _resetState() {
        document.getElementById("ai-gen-input").value = "";
        document.getElementById("ai-gen-error").style.display = "none";
        document.getElementById("ai-gen-explanation").style.display = "none";
        document.getElementById("ai-gen-reasoning").style.display = "none";
        document.getElementById("ai-gen-review-hint").style.display = "none";
        document.getElementById("ai-gen-progress").style.display = "none";
    }

    _injectStyles() {
        if (document.getElementById("ai-gen-styles")) return;

        const style = document.createElement("style");
        style.id = "ai-gen-styles";
        style.textContent = `
            #ai-gen-overlay {
                display: none;
                position: fixed; inset: 0; z-index: 9999;
                background: rgba(0,0,0,0.6);
                backdrop-filter: blur(4px);
                align-items: center; justify-content: center;
            }
            .ai-gen-dialog {
                background: #1e1e2e; border-radius: 16px;
                width: 600px; max-width: 90vw;
                box-shadow: 0 25px 50px -12px rgba(0,0,0,0.5);
                border: 1px solid #3f3f5f;
                font-family: 'Segoe UI', system-ui, sans-serif;
                color: #e2e8f0;
                overflow: hidden;
                display: flex; flex-direction: column;
            }
            .ai-gen-header {
                padding: 18px 24px;
                background: #252538;
                border-bottom: 1px solid #3f3f5f;
                display: flex; justify-content: space-between; align-items: center;
            }
            .ai-gen-title { 
                font-size: 18px; font-weight: 600; 
                background: linear-gradient(135deg, #a78bfa, #f472b6);
                -webkit-background-clip: text;
                -webkit-text-fill-color: transparent;
            }
            .ai-gen-close {
                background: none; border: none; color: #94a3b8;
                font-size: 24px; cursor: pointer; padding: 4px; line-height: 1;
                transition: color 0.2s;
            }
            .ai-gen-close:hover { color: #f1f5f9; }
            
            .ai-gen-body { padding: 24px; display: flex; flex-direction: column; gap: 16px; }
            .ai-gen-tip { color: #94a3b8; font-size: 14px; line-height: 1.5; }
            
            .ai-gen-examples { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
            .ai-gen-examples-label { font-size: 12px; color: #64748b; }
            .ai-gen-example-btn {
                background: #2d2d44; border: 1px solid #3f3f5f;
                color: #cbd5e1; font-size: 12px; padding: 4px 10px;
                border-radius: 20px; cursor: pointer;
                transition: all 0.2s;
            }
            .ai-gen-example-btn:hover { background: #3f3f5f; border-color: #6366f1; color: #fff; }
            
            .ai-gen-textarea {
                background: #0f172a; border: 1px solid #3f3f5f;
                border-radius: 8px; color: #f1f5f9; padding: 12px;
                font-size: 14px; line-height: 1.6; resize: none;
                width: 100%; box-sizing: border-box;
                transition: border-color 0.2s;
            }
            .ai-gen-textarea:focus { outline: none; border-color: #6366f1; }
            
            .ai-gen-progress {
                display: flex; align-items: center; gap: 12px;
                background: rgba(99, 102, 241, 0.1);
                border: 1px solid rgba(99, 102, 241, 0.3);
                padding: 12px 16px; border-radius: 8px;
            }
            .ai-gen-spinner {
                width: 18px; height: 18px;
                border: 2px solid #6366f1;
                border-top-color: transparent;
                border-radius: 50%;
                animation: ai-gen-spin 0.8s linear infinite;
            }
            #ai-gen-progress-text { font-size: 13px; color: #a5b4fc; }
            
            .ai-gen-explanation {
                background: #161b22; border-left: 4px solid #a78bfa;
                padding: 12px 16px; border-radius: 4px;
            }
            .ai-gen-explanation-label { font-size: 12px; font-weight: 600; color: #a78bfa; margin-bottom: 6px; }
            .ai-gen-explanation-content { font-size: 13px; line-height: 1.6; color: #cbd5e1; }

            .ai-gen-reasoning {
                background: #161b22; border-left: 4px solid #fbbf24;
                padding: 12px 16px; border-radius: 4px;
            }
            .ai-gen-reasoning-label { font-size: 12px; font-weight: 600; color: #fbbf24; margin-bottom: 6px; }
            .ai-gen-reasoning-content {
                font-size: 12px; line-height: 1.6; color: #94a3b8;
                white-space: pre-wrap; max-height: 300px; overflow-y: auto;
                padding-top: 8px; border-top: 1px solid #3f3f5f; margin-top: 8px;
            }
            
            .ai-gen-error {
                color: #f87171; background: rgba(248, 113, 113, 0.1);
                padding: 10px 14px; border-radius: 6px; font-size: 13px;
                border: 1px solid rgba(248, 113, 113, 0.2);
            }
            .ai-gen-review-hint { color: #fbbf24; font-size: 12px; font-style: italic; }
            
            .ai-gen-footer {
                padding: 16px 24px; background: #252538;
                border-top: 1px solid #3f3f5f;
                display: flex; justify-content: flex-end; gap: 12px;
            }
            .ai-gen-btn-cancel {
                background: transparent; border: 1px solid #3f3f5f;
                color: #94a3b8; padding: 0 16px; height: 36px;
                border-radius: 6px; cursor: pointer; font-size: 14px;
            }
            .ai-gen-btn-cancel:hover { background: #2d2d44; color: #f1f5f9; }
            
            .ai-gen-btn-generate {
                background: linear-gradient(135deg, #6366f1, #8b5cf6);
                border: none; color: white; padding: 0 20px; height: 36px;
                border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 500;
                box-shadow: 0 4px 12px rgba(99, 102, 241, 0.3);
            }
            .ai-gen-btn-generate:hover { 
                transform: translateY(-1px);
                box-shadow: 0 6px 16px rgba(99, 102, 241, 0.4);
            }
            .ai-gen-btn-generate:disabled {
                background: #3f3f5f; color: #64748b; cursor: not-allowed;
                transform: none; box-shadow: none;
            }
            
            @keyframes ai-gen-spin { to { transform: rotate(360deg); } }
        `;
        document.head.appendChild(style);
    }
}
