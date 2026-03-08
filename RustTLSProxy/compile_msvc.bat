@echo off
echo "    ____                    __   ______  __    _____    ____                                 "
echo "   / __ \  __  __   _____  / /_ /_  __/ / /   / ___/   / __ \   _____  ____    _  __   __  __"
echo "  / /_/ / / / / /  / ___/ / __/  / /   / /    \__ \   / /_/ /  / ___/ / __ \  | |/_/  / / / /"
echo " / _, _/ / /_/ /  (__  ) / /_   / /   / /___ ___/ /  / ____/  / /    / /_/ / _>  <   / /_/ / "
echo "/_/ |_|  \__,_/  /____/  \__/  /_/   /_____//____/  /_/      /_/     \____/ /_/|_|   \__, /  "
echo "                                                                                    /____/   "

set "PROJECT_DIR=%~1"
if "%PROJECT_DIR%"=="" set "PROJECT_DIR=%~dp0"
echo [INFO] Cargo工作目录: %PROJECT_DIR%
pushd "%PROJECT_DIR%"

:: 1. 检测 Cargo 是否已安装
where cargo >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] 未找到 cargo 命令。请确保已安装 Rust 环境并添加到系统 PATH 中。
    exit /b 1
)

:: 2. 检测 Rust 编译目标 (x86_64-pc-windows-msvc) 是否已安装
rustup target list --installed | findstr /c:"x86_64-pc-windows-msvc" >nul
if %errorlevel% neq 0 (
    echo [WARNING] 未检测到 x86_64-pc-windows-msvc 目标，正在安装...
    rustup target add x86_64-pc-windows-msvc
    if %errorlevel% neq 0 (
        echo [ERROR] 自动安装 target 失败，请手动运行: rustup target add x86_64-pc-windows-msvc
        exit /b 1
    )
)

:: 3. 清理旧环境
echo [INFO] 清理旧编译产物...
if exist "target" rmdir /s /q target
if exist "RustTLSProxy.dll" del RustTLSProxy.dll

:: 4. 编译
echo [INFO] 开始执行 Cargo Release 编译...
cargo build --release --target x86_64-pc-windows-msvc
if %errorlevel% neq 0 (
    echo [ERROR] Cargo 编译过程中出错。
    exit /b 1
)

:: 5. 复制 DLL 到 RustTLSProxy 根目录
echo [INFO] 移动 DLL 到当前目录...
copy /Y "target\x86_64-pc-windows-msvc\release\RustTLSProxy.dll" "RustTLSProxy.dll"
if %errorlevel% neq 0 (
    echo [ERROR] 复制 DLL 到项目目录失败。
    exit /b 1
)

echo [SUCCESS] Rust 模块已就绪。
exit /b 0