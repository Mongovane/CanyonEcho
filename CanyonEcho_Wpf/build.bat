::CanyonEcho_Wpf 安装单文件打包插件 Costura.Fody
dotnet add package Costura.Fody

::CanyonEchoUpdate 安装 Costura.Fody
cd ..
cd CanyonEchoUpdate
dotnet add package Costura.Fody

::Release 模式生成单文件 exe
cd ..
cd CanyonEcho_Wpf
dotnet build --configuration Release

::清理临时包引用
cd ..
cd CanyonEchoUpdate
dotnet remove package Costura.Fody

cd ..
cd CanyonEcho_Wpf
dotnet remove package Costura.Fody

pause
