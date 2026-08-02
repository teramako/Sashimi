<#
.SYNOPSIS
    Tests Test-RawCommand
#>

Describe 'Test-RawCommand' {
    Context 'Basic' {
        It '<cmd> => <expected>' -ForEach @(
            @{ cmd = '/bin/true'; expected = $true }
            @{ cmd = '/bin/false'; expected = $false }
        ) {
            $result = Test-RawCommand $cmd
            $result | Should -BeExactly $expected
        }
    }

    Context 'Outputs' {
        BeforeAll {
            $cmd = Join-Path -Path $PSScriptRoot -ChildPath assets,redirect_test.sh
            $Script:result = Test-RawCommand -AsString $cmd -InformationVariable info
        }

        It 'Result should be $true' {
            $result | Should -BeExactly $true
        }

        It 'stdout should be set' {
            $info.Where({$_.Tags -in "Stdout"}).MessageData.Value |
                Should -BeExactly "StdOut"
        }

        It 'stderr should be set' {
            $info.Where({$_.Tags -in "Stderr"}).MessageData.Value |
                Should -BeExactly "StdErr"
        }
    }
}
