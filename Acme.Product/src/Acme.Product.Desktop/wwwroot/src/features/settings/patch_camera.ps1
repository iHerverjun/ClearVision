# 更新 settingsView.js 相机管理中的静态表格数据为动态加载和默认状态

$file = 'c:\Users\11234\Desktop\ClearVision\Acme.Product\src\Acme.Product.Desktop\wwwroot\src\features\settings\settingsView.js'
$content = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)

# 匹配表格区域 <tbody> ... </tbody>
$patternTBody = '(?s)<tbody>\s*<!-- Demo Row \(will be overridden by refreshCameraTable if bindings exist\) -->\s*<tr class="camera-row">.*?</tr>\s*</tbody>'
$replacementTBody = "<tbody>`r`n                            <!-- 动态加载由 loadCameraBindings() 注入 -->`r`n                            <tr><td colspan=`"5`" style=`"text-align:center; padding: 24px; color:var(--text-muted);`"><div class=`"cv-spinner`" style=`"margin-right:8px; display:inline-block;`"></div>正在加载相机配置...</td></tr>`r`n                        </tbody>"

$content = [regex]::Replace($content, $patternTBody, $replacementTBody)

# 匹配并替换下方的参数配置卡片（Parameters Card），将 Top_Cam_01 替换为占位
$content = $content.Replace("<span>参数配置: Top_Cam_01</span>", "<span>参数配置: <span id='current-cam-name'>未选中相机</span></span>")
$content = $content.Replace("5000", "") # 清空曝光时间的默认值
$content = $content.Replace("1.0", "") # 清空增益
$content = $content.Replace("30", "") #帧率
$content = $content.Replace("2448", "") #宽度
$content = $content.Replace("2048", "") #高度

[System.IO.File]::WriteAllText($file, $content, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Replaced static camera data successfully!"
