
dotnet publish MapExportExtension\MapExportExtension.csproj -r win-x64 -o ".\@arma3MapExporter"

cd "@arma3MapExporter"

.\hemtt.exe build

cd ..
