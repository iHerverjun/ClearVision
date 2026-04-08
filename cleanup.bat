@echo off
chcp 65001 > nul
echo ==========================================
echo  ClearVision 项目清理脚本
echo ==========================================
echo.

set "SCRIPT_DIR=%~dp0"
set "PROJECT_ROOT=%SCRIPT_DIR:~0,-1%"
set "ACME_DIR=%PROJECT_ROOT%\Acme.Product"

echo [1/3] 正在清理根目录的构建日志...
cd /d "%PROJECT_ROOT%"
if exist "build_*.txt" del /F /Q "build_*.txt" 2>nul
if exist "build*.log" del /F /Q "build*.log" 2>nul
if exist "build_diag*.txt" del /F /Q "build_diag*.txt" 2>nul
if exist "build_error*.txt" del /F /Q "build_error*.txt" 2>nul
if exist "build_full*.txt" del /F /Q "build_full*.txt" 2>nul
if exist "build_output*.txt" del /F /Q "build_output*.txt" 2>nul
if exist "build_sln*.txt" del /F /Q "build_sln*.txt" 2>nul
echo √ 根目录日志文件已清理

echo.
echo [2/3] 正在清理 Acme.Product 目录的构建日志...
cd /d "%ACME_DIR%"
if exist "build*.txt" del /F /Q "build*.txt" 2>nul
if exist "build*.log" del /F /Q "build*.log" 2>nul
if exist "msbuild.log" del /F /Q "msbuild.log" 2>nul
if exist "run.log" del /F /Q "run.log" 2>nul
echo √ Acme.Product 目录日志文件已清理

echo.
echo [3/3] 正在清理发布目录...
cd /d "%PROJECT_ROOT%"
if exist "publish" (
    rmdir /S /Q "publish" 2>nul
    echo √ publish 目录已删除
) else (
    echo - publish 目录不存在，跳过
)

echo.
echo ==========================================
echo  清理完成！
echo ==========================================
echo.
echo 按任意键退出...
pause > nul
