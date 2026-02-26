$file = 'c:\Users\11234\Desktop\ClearVision\Acme.Product\src\Acme.Product.Desktop\wwwroot\src\features\settings\settingsView.js'
$content = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)

# 匹配激活菜单时检查如果是 users 执行的操作，加上如果是 cameras 时查询绑定配置的逻辑
$pattern = '(?s)if \(tabName === .users. && this\.isAdmin\) {\s*this\.refreshUserTable\(\);\s*}'
$replacement = @"
if (tabName === 'users' && this.isAdmin) {
            this.refreshUserTable();
        } else if (tabName === 'cameras') {
            this.loadCameraBindings();
        }
"@

$newContent = [regex]::Replace($content, $pattern, $replacement)
[System.IO.File]::WriteAllText($file, $newContent, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'Done'
