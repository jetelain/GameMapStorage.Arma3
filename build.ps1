
dotnet publish MapExportExtension\MapExportExtension.csproj -r win-x64 -o ".\@arma3MapExporter"

dotnet publish MapExportLauncher\MapExportLauncher.csproj -r win-x64 -o ".\bin"

cd "@arma3MapExporter"

.\hemtt.exe build

cd ..
