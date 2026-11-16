$ErrorActionPreference = 'Stop'
$framework = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$refs = @('System.dll','System.Core.dll','System.Xml.dll','System.Xaml.dll','System.Drawing.dll','System.Windows.Forms.dll','WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll')
$argsList = @('/nologo','/target:winexe','/optimize+','/out:SideNote.exe','/utf8output','/win32icon:assets/icon.ico')
foreach ($ref in $refs) {
    $path = Join-Path $framework $ref
    if (!(Test-Path $path)) { $path = Join-Path "$framework\WPF" $ref }
    $argsList += "/reference:$path"
}
& "$framework\csc.exe" @argsList App.cs
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
