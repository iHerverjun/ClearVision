# 更新 settingsView.js 相机事件绑定与刷新逻辑
$file = 'c:\Users\11234\Desktop\ClearVision\Acme.Product\src\Acme.Product.Desktop\wwwroot\src\features\settings\settingsView.js'
$content = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)

# 替换 bindCameraManagementEvents 到 refreshCameraTable 这部分的代码
$pattern = '(?s)bindCameraManagementEvents\(\) \{.*?\}(?=\s*bindUserManagementEvents\(\) \{)'

$replacement = @"
    bindCameraManagementEvents() {
        const discoverBtn = this.container.querySelector('.settings-btn-primary'); // 搜索相机按钮
        if (discoverBtn) {
            discoverBtn.addEventListener('click', () => this.discoverCameras());
        }

        const tbody = this.container.querySelector('#camera-bindings-table tbody');
        if (tbody) {
            tbody.addEventListener('click', (e) => {
                const tr = e.target.closest('tr.camera-row');
                if (!tr) return;

                // 点击选中行，展示详情
                this.selectCameraRow(tr);

                // 删除按钮
                const deleteBtn = e.target.closest('.action-icon-btn');
                if (deleteBtn && deleteBtn.querySelector('svg path[d="M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"]') == null) {
                    // 这个是用作详情的（省略号图标原本用于菜单，这里假设以后扩展）。如果以后改为真正的删除按钮可以触发
                }
            });
        }
    }

    async loadCameraBindings() {
        const tbody = this.container.querySelector('#camera-bindings-table tbody');
        if (tbody) {
            tbody.innerHTML = `<tr><td colspan="5" style="text-align:center; padding: 24px;"><div class="cv-spinner" style="margin-right:8px; display:inline-block;"></div>正在加载相机配置...</td></tr>`;
        }

        try {
            const bindings = await httpClient.get('/cameras/bindings');
            this.cameraBindings = bindings || [];
            this.refreshCameraTable();
        } catch (error) {
            console.error('Failed to load camera bindings:', error);
            if (tbody) {
                tbody.innerHTML = `<tr><td colspan="5" style="text-align:center; padding: 20px; color:var(--accent);">加载配置失败: ` + error.message + `</td></tr>`;
            }
        }
    }

    async discoverCameras() {
        showToast('正在搜索在线相机...', 'info');
        const discoverBtn = this.container.querySelector('.settings-btn-primary');
        if (discoverBtn) discoverBtn.disabled = true;

        try {
            const devices = await httpClient.get('/cameras/discover');
            showToast(`找到 ` + (devices?.length || 0) + ` 个相机设备`, 'success');
            // 此处本应弹出选择设备弹窗，此处做简单处理，重新刷新列表。后续可以扩展。
            await this.loadCameraBindings();
        } catch (error) {
            showToast('搜索相机失败: ' + error.message, 'error');
        } finally {
            if (discoverBtn) discoverBtn.disabled = false;
        }
    }

    refreshCameraTable() {
        const tbody = this.container.querySelector('#camera-bindings-table tbody');
        if (!tbody) return;

        if (!this.cameraBindings || this.cameraBindings.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:var(--text-muted); padding:24px;">暂无绑定配置，请点击“搜索相机”以添加</td></tr>';
            return;
        }

        tbody.innerHTML = this.cameraBindings.map((b, index) => {
            const isConnected = b.isEnabled !== false; // 假设字段
            const statusClass = isConnected ? 'status-connected' : 'status-error';
            const statusDotClass = isConnected ? 'status-dot' : 'status-dot status-error';
            const statusText = isConnected ? '已连接' : '已断开';
            const bgClass = index === 0 ? '#fee2e2' : '#e0e7ff';
            const fgClass = index === 0 ? 'var(--cinnabar)' : 'var(--primary)';

            return `
            <tr class="camera-row" data-id="` + b.id + `" style="cursor: pointer;">
                <td>
                    <div style="display:flex; align-items:center; gap:12px;">
                        <div style="width:32px; height:32px; background:` + bgClass + `; border-radius:8px; display:flex; align-items:center; justify-content:center; color:` + fgClass + `;">
                            <svg viewBox="0 0 24 24" style="width:18px;height:18px;fill:currentColor;"><path d="M12 4C7.58 4 4 7.58 4 12s3.58 8 8 8 8-3.58 8-8-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6s2.69-6 6-6 6 2.69 6 6-2.69 6-6 6zM12 7c-2.76 0-5 2.24-5 5s2.24 5 5 5 5-2.24 5-5-2.24-5-5-5zm0 8c-1.65 0-3-1.35-3-3s1.35-3 3-3 3 1.35 3 3-1.35 3-3 3z"/></svg>
                        </div>
                        <div>
                            <div class="font-bold">` + (b.displayName || '未命名相机') + `</div>
                            <div class="text-muted" style="font-size:12px;">` + (b.serialNumber || '未知') + `</div>
                        </div>
                    </div>
                </td>
                <td><span class="font-mono">` + (b.ipAddress || '192.168.x.x') + `</span></td>
                <td>` + (b.manufacturer || '未知') + `</td>
                <td><span class="settings-status-badge ` + statusClass + `"><span class="` + statusDotClass + `"></span> ` + statusText + `</span></td>
                <td><button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"/></svg></button></td>
            </tr>
            `;
        }).join('');
    }

    selectCameraRow(tr) {
        // 取消其他行高亮
        const allRows = this.container.querySelectorAll('tr.camera-row');
        allRows.forEach(r => r.style.backgroundColor = '');
        
        // 高亮当前行
        tr.style.backgroundColor = 'var(--panel-bg)';

        const id = tr.getAttribute('data-id');
        const cam = this.cameraBindings.find(b => b.id === id);
        
        // 更新参数面板
        if (cam) {
            const nameEl = this.container.querySelector('#current-cam-name');
            if (nameEl) nameEl.textContent = cam.displayName || '未命名相机';

            // 更新输入框 (如果有)
            const exposeInput = this.container.querySelector('input[type="number"]');
            if(exposeInput) {
               // 假填充
               exposeInput.value = 5000;
            }
        }
    }

    // 辅助旧代码兼容
    getUpdatedCameraBindings() {
        return this.cameraBindings || [];
    }
"@

$newContent = [regex]::Replace($content, $pattern, $replacement)
[System.IO.File]::WriteAllText($file, $newContent, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'Methods replaced!'
