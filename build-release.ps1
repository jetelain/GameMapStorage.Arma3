
dotnet publish MapExportExtension\MapExportExtension.csproj -c Release -r win-x64 -o ".\@arma3MapExporter"

dotnet publish MapExportLauncher\MapExportLauncher.csproj -c Release -r win-x64 -o ".\bin"

cd "@arma3MapExporter"

.\hemtt.exe release

cd ..
