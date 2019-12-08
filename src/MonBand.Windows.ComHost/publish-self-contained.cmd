@Echo Off
cd /D %~dp0
dotnet publish -r win-x64 -c Release --self-contained -o bin\Release\netcoreapp3.1\publish
