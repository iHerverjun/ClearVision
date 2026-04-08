@echo off
set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%"
dotnet build "Acme.Product\src\Acme.Product.Application\Acme.Product.Application.csproj" > "%SCRIPT_DIR%build_output.txt"
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
