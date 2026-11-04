param([string]$Version = '1.0.5')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Publish-Asi.ps1') -Version $Version
