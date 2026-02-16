/**
 * HTTP API 客户端
 * 用于与后端 Minimal APIs 通信
 */

class HttpClient {
    constructor(baseUrl = null) {
        this._baseUrl = baseUrl;
        this._defaultHeaders = {
            'Content-Type': 'application/json'
        };
        this._discoveredPort = null;
    }

    /**
     * 获取请求头（自动附加认证 Token）
     * 每次请求时动态读取 localStorage 中的 Token
     */
    get defaultHeaders() {
        const headers = { ...this._defaultHeaders };
        const token = localStorage.getItem('cv_auth_token');
        if (token) {
            headers['Authorization'] = `Bearer ${token}`;
        }
        return headers;
    }

    set defaultHeaders(value) {
        this._defaultHeaders = value;
    }

    /**
     * 获取基础 URL
     * 优先级：1. 构造参数 2. window.__API_BASE_URL__ 注入 3. 自动探测
     */
    get baseUrl() {
        if (this._baseUrl) return this._baseUrl;
        if (window.__API_BASE_URL__) return window.__API_BASE_URL__;

        const { protocol, hostname, port } = window.location;

        // 如果是在 WebView2 (file://) 或 Electron 环境下运行
        // 或者使用虚拟主机 app.local
        if (protocol === 'file:' || protocol === 'chrome-extension:' || hostname === 'app.local') {
            // 尝试从本地存储获取上次成功连接的端口
            const savedPort = localStorage.getItem('cv_api_port');
            if (savedPort) {
                console.log(`[HttpClient] 使用本地存储的端口: ${savedPort}`);
                return `http://localhost:${savedPort}/api`;
            }

            // 如果已经发现过端口，使用发现的端口
            if (this._discoveredPort) {
                return `http://localhost:${this._discoveredPort}/api`;
            }

            // 默认回退到 localhost:5000
            console.warn('[HttpClient] 警告: 未检测到 API 配置，将尝试自动发现端口');
            return 'http://localhost:5000/api';
        }

        // 浏览器环境：使用当前页面端口
        return `${protocol}//${hostname}:${port || 5000}/api`;
    }

    /**
     * 自动发现可用端口
     * 尝试连接 5000-5010 端口，找到实际运行的后端服务
     */
    async discoverPort() {
        if (this._discoveredPort) return this._discoveredPort;

        const testPorts = [5000, 5001, 5002, 5003, 5004, 5005];

        for (const port of testPorts) {
            try {
                const controller = new AbortController();
                const timeoutId = setTimeout(() => controller.abort(), 500);

                const response = await fetch(`http://localhost:${port}/health`, {
                    method: 'GET',
                    signal: controller.signal
                });

                clearTimeout(timeoutId);

                if (response.ok) {
                    console.log(`[HttpClient] 发现后端服务运行在端口: ${port}`);
                    this._discoveredPort = port;
                    localStorage.setItem('cv_api_port', port.toString());
                    return port;
                }
            } catch (e) {
                // 端口不可用，继续尝试下一个
            }
        }

        return null;
    }

    /**
     * 保存成功连接的端口
     */
    saveSuccessfulPort(url) {
        try {
            const match = url.match(/:(\d+)\/api/);
            if (match) {
                localStorage.setItem('cv_api_port', match[1]);
                console.log(`[HttpClient] 已保存 API 端口: ${match[1]}`);
            }
        } catch (e) {
            // 忽略存储错误
        }
    }

    /**
     * 发送 GET 请求
     */
    async get(url, params = null) {
        const queryString = params ? '?' + new URLSearchParams(params).toString() : '';
        let fullUrl = this.baseUrl + url + queryString;

        console.log(`[HttpClient] GET ${fullUrl}`);

        try {
            const response = await fetch(fullUrl, {
                method: 'GET',
                headers: this.defaultHeaders
            });
            this.saveSuccessfulPort(fullUrl);
            return this.handleResponse(response);
        } catch (error) {
            // 如果是连接错误，尝试自动发现端口并重试
            if (error.message?.includes('Failed to fetch') || error.name === 'TypeError') {
                const discoveredPort = await this.discoverPort();
                if (discoveredPort && discoveredPort !== 5000) {
                    console.log(`[HttpClient] 尝试使用发现的端口 ${discoveredPort} 重试...`);
                    fullUrl = `http://localhost:${discoveredPort}/api${url}${queryString}`;
                    const response = await fetch(fullUrl, {
                        method: 'GET',
                        headers: this.defaultHeaders
                    });
                    this.saveSuccessfulPort(fullUrl);
                    return this.handleResponse(response);
                }
            }
            throw this.handleNetworkError(error, fullUrl);
        }
    }

    /**
     * 发送 POST 请求
     */
    async post(url, data = null) {
        let fullUrl = this.baseUrl + url;
        console.log(`[HttpClient] POST ${fullUrl}`);

        try {
            const response = await fetch(fullUrl, {
                method: 'POST',
                headers: this.defaultHeaders,
                body: data ? JSON.stringify(data) : null
            });
            this.saveSuccessfulPort(fullUrl);
            return this.handleResponse(response);
        } catch (error) {
            // 如果是连接错误，尝试自动发现端口并重试
            if (error.message?.includes('Failed to fetch') || error.name === 'TypeError') {
                const discoveredPort = await this.discoverPort();
                if (discoveredPort && discoveredPort !== 5000) {
                    console.log(`[HttpClient] 尝试使用发现的端口 ${discoveredPort} 重试...`);
                    fullUrl = `http://localhost:${discoveredPort}/api${url}`;
                    const response = await fetch(fullUrl, {
                        method: 'POST',
                        headers: this.defaultHeaders,
                        body: data ? JSON.stringify(data) : null
                    });
                    this.saveSuccessfulPort(fullUrl);
                    return this.handleResponse(response);
                }
            }
            throw this.handleNetworkError(error, fullUrl);
        }
    }

    /**
     * 发送 PUT 请求
     */
    async put(url, data = null) {
        const fullUrl = this.baseUrl + url;
        const response = await fetch(fullUrl, {
            method: 'PUT',
            headers: this.defaultHeaders,
            body: data ? JSON.stringify(data) : null
        });
        return this.handleResponse(response);
    }

    /**
     * 发送 DELETE 请求
     */
    async delete(url) {
        const fullUrl = this.baseUrl + url;
        const response = await fetch(fullUrl, {
            method: 'DELETE',
            headers: this.defaultHeaders
        });
        return this.handleResponse(response);
    }

    /**
     * 处理网络错误
     * 提供清晰的错误提示
     */
    handleNetworkError(error, url) {
        if (error.name === 'TypeError' && error.message.includes('Failed to fetch')) {
            const apiUrl = new URL(url);
            const errorMessage = `
🔴 无法连接到后端服务 (${apiUrl.host})

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📋 问题诊断：

1️⃣ 后端服务未运行
   • 你需要先启动 Acme.Product.Desktop 项目
   • 在 Visual Studio 中按 F5 运行

2️⃣ 后端运行在其他端口
   • 当前尝试端口: ${apiUrl.port}
   • 后端可能运行在 5001-5005 范围

3️⃣ 防火墙/安全软件阻止
   • 检查 Windows Defender 或其他安全软件

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔧 快速修复：

方式1 - 启动后端服务：
   cd Acme.Product/src/Acme.Product.Desktop
   dotnet run

方式2 - 临时指定端口（浏览器控制台执行）：
   localStorage.setItem('cv_api_port', '5001');
   location.reload();

方式3 - 检查后端实际端口：
   在 Visual Studio "输出" 窗口查看 Web 服务器启动日志
            `.trim();
            console.error('[HttpClient] 连接失败:', errorMessage);
            return new Error(errorMessage);
        }
        return error;
    }

    /**
     * 处理响应
     */
    async handleResponse(response) {
        if (!response.ok) {
            const error = await response.text();
            throw new Error(error || `HTTP ${response.status}`);
        }

        const contentType = response.headers.get('content-type');
        if (contentType && contentType.includes('application/json')) {
            return await response.json();
        }

        return await response.text();
    }
}

// 创建默认实例
const httpClient = new HttpClient();

export default httpClient;
export { HttpClient };
