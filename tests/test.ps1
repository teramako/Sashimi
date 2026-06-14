<#
.SYNOPSIS
    Test Sashimi

.PARAMETER FullName
    Filter names
#>
param(
    [Parameter()]
    [string[]] $FullName
)

$ParnetDir = Join-Path -Path $PSScriptRoot -ChildPath .. -Resolve
Import-Module (Join-Path -Path $ParnetDir -ChildPath Sashimi.psd1)
Import-Module -Name Pester

$conf = New-PesterConfiguration
if ($FullName.Count -gt 0)
{
    $conf.Filter.FullName = $FullName
}
$conf.Run.Path = $PSScriptRoot
$conf.Output.Verbosity = 'Detailed'

Invoke-Pester -Configuration $conf

