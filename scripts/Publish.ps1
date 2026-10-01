param([string]$Version = '0.4.0-asi.2')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Publish-Asi.ps1') -Version $Version
