param(
    [string]$ConfigPath,
    [string]$TargetDll
)

$dotnetRoot = if ($env:DOTNET_ROOT) { $env:DOTNET_ROOT } else { "C:\Program Files\dotnet" }
$wpfDir = Join-Path $dotnetRoot "shared\Microsoft.WindowsDesktop.App"
$latestWpf = Get-ChildItem $wpfDir -Directory | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName

$outDir = [System.IO.Path]::GetDirectoryName($TargetDll) + "\Obfuscated"

$inDir = [System.IO.Path]::GetDirectoryName($TargetDll)

$xml = '<?xml version="1.0"?>'
$xml += '<Obfuscator>'
$xml += '<Var name="InPath" value="' + $inDir + '" />'
$xml += '<Var name="OutPath" value="' + $outDir + '" />'
$xml += '<AssemblySearchPath path="' + $latestWpf + '" />'
$xml += '<Module file="' + $TargetDll + '">'
$xml += '<SkipType name="*View" />'
$xml += '<SkipType name="*Converter" />'
$xml += '<SkipType name="App" />'
$xml += '<SkipType name="MainWindow" />'
$xml += '<SkipType name="NotifyBase" />'
$xml += '<SkipType name="RelayCommand" />'
$xml += '</Module>'
$xml += '</Obfuscator>'

Set-Content -Path $ConfigPath -Value $xml -Encoding UTF8 -NoNewline
