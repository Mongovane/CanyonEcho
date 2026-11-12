@echo off
chcp 65001 >nul
title 峡谷回音 (CanyonEcho v4.2.0) 一键编译打包单文件 EXE 工具
echo ======================================================================
echo   正在为您编译打包【峡谷回音 CanyonEcho v4.2.0】独立单文件可执行程序...
echo ======================================================================
echo.

where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [错误] 未检测到 .NET SDK (dotnet 命令)。
    echo 请先安装 Visual Studio 2022 或 .NET Desktop SDK: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo [1/3] 正在还原 NuGet 依赖包 (HandyControl / Prism / WindowsInput / Costura.Fody)...
dotnet restore CanyonEcho.sln
if %errorlevel% neq 0 (
    echo [错误] NuGet 包还原失败，请检查网络连接！
    pause
    exit /b 1
)

echo.
echo [2/3] 正在以 Release 模式编译并合并所有 DLL 为单一独立 EXE...
dotnet build CanyonEcho.sln --configuration Release
if %errorlevel% neq 0 (
    echo [错误] 编译失败，请查看上方报错信息！
    pause
    exit /b 1
)

echo.
echo [3/3] 正在整理输出产物到 Release_EXE 文件夹...
if not exist "Release_EXE" mkdir "Release_EXE"
copy /Y "CanyonEcho_Wpf\bin\Release\net472\峡谷回音.exe" "Release_EXE\峡谷回音.exe" >nul

echo.
echo ======================================================================
echo   ✅ 编译成功！独立可执行文件已生成至：
echo      %~dp0Release_EXE\峡谷回音.exe
echo   （已通过 Costura.Fody 内嵌全部依赖，可直接复制到任意电脑双击运行）
echo ======================================================================
explorer "Release_EXE"
pause
