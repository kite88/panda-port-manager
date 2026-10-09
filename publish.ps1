<#
 .SYNOPSIS
   一次打出 Panda Port Manager 的 win-x64 / win-x86 / win-arm64 三个包。

 .PARAMETER SelfContained
   加 -SelfContained 则打包为免运行时（自带 .NET 10）的绿色单文件，体积更大；
   默认（不加）为依赖框架发布，目标机器需已装 .NET 10 运行时。

 .EXAMPLE
   .\publish.ps1                  # 依赖框架，三架构单文件
   .\publish.ps1 -SelfContained   # 自带运行时，三架构单文件
#>
[CmdletBinding()]
param(
    [switch] $SelfContained
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'PandaPortManager\PandaPortManager.csproj'
$outRoot = Join-Path $root 'publish'

if (-not (Test-Path $proj)) { throw "找不到工程文件: $proj" }

$sc = if ($SelfContained) { 'true' } else { 'false' }
$rids = @('win-x64', 'win-x86', 'win-arm64')

Write-Host "配置: PublishSingleFile=true, SelfContained=$sc" -ForegroundColor Cyan
Write-Host "目标架构: $($rids -join ', ')`n"

foreach ($rid in $rids) {
    $out = Join-Path $outRoot $rid
    Write-Host "==> 发布 $rid -> $out" -ForegroundColor Yellow
    dotnet publish $proj -c Release -r $rid -p:PublishSingleFile=true -p:SelfContained=$sc -p:IncludeNativeLibrariesForSelfExtract=true -o $out
    if ($LASTEXITCODE -ne 0) { throw "发布失败: $rid" }
}

Write-Host "`n完成。产物清单:" -ForegroundColor Green
Get-ChildItem $outRoot -Directory | ForEach-Object {
    $exe = Join-Path $_.FullName 'PandaPortManager.exe'
    if (Test-Path $exe) {
        $sz = (Get-Item $exe).Length / 1MB
        Write-Host ('  {0,-12} {1,8:N2} MB' -f $_.Name, $sz)
    }
}
