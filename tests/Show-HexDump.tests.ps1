<#
.SYNOPSIS
    Tests Show-HexDump
#>
Describe 'Show-HexDump' {
    Context 'Basic' {
        It 'Data parameter (<encoding>)' -ForEach @(
            @{ encoding = 'ASCII';     data = [byte[]]@(0x00..0xFF);
               expectedCount = 256; expectedCharCount = 128 }
            @{ encoding = 'Latin1';    data = [byte[]]@(0x00..0xFF);
               expectedCount = 256; expectedCharCount = 256 }
            @{ encoding = 'UTF8';      data = [System.Text.Encoding]::UTF8.GetBytes('あいうえお');
               expectedCount = 15;  expectedCharCount = 5 }
            @{ encoding = 'SHIFT_JIS'; data = [System.Text.Encoding]::GetEncoding('shift_jis').GetBytes('あいうえお');
               expectedCount = 10;  expectedCharCount = 5 }
        ) {
            $result = Show-HexDump -Data $data -Encoding $encoding
            $charData = $result.ListCharData

            $charData.Count | Should -Be $expectedCount
            $charData.Where({$_.IsChar}).Count | Should -Be $expectedCharCount
        }
        It 'Pipeline input: <name>' -ForEach @(
            @{ name = 'Block of bytes'; flag = $true }
            @{ name = '1-byte at a time'; flag = $false }
        ) {
            $result = Write-Output -NoEnumerate:$flag @(0x00..0x7F) | Show-HexDump -Encoding ASCII
            $result.Count | Should -Be 8
            $result.ListCharData.Count | Should -Be 128
        }

        It 'Path parameter' {
            $file = Join-Path $PSScriptRoot 'assets','ascii.bin'
            $result = Show-HexDump -Path $file -Encoding ASCII

            $result.Count | Should -Be 16
            $result.ListCharData.Count | Should -Be 256
        }
    }

    Context 'Cancel' {
        It 'Cancel after 200 milliseconds' {
            if ($IsWindows) {
                Set-ItResult -Skipped -Because '/dev/urandom is not available on Windows'
            }

            $job = Start-Job { Show-HexDump -Path /dev/urandom -Length 50mb }
            Start-Sleep -Milliseconds 200
            Stop-Job $job
            Receive-Job $job -Wait -AutoRemoveJob | Out-Null
            $job.State | Should -Be 'Stopped'
        }
    }

    Context 'Fallback' {
        BeforeAll {
            function BytesToString([byte[]] $data) {
                return $data.ForEach({ '{0:X2}' -f $_ }) -join ' '
            }
        }

        It 'handles fallback at buffer boundary: <Encoding>' -ForEach @(
            @{ Encoding = 'ascii' }
            @{ Encoding = 'latin1' }
            @{ Encoding = 'utf-8' }
            @{ Encoding = 'euc-jp' }
        ) {
            $bytes = [byte[]]::new(1020) + @(0xAA, 0xC4, 0x14, 0xF1, 0xD7, 0x0A)
            $expected = BytesToString ($bytes | Select-Object -Last 18)

            $resultBytes = (Show-HexDump -Data $bytes -Encoding $Encoding).ListCharData.B | Select-Object -Last 18
            BytesToString $resultBytes | Should -BeExactly $expected
        }

        It 'handles fallback twice: <Encoding>' -ForEach @(
            @{ Encoding = 'euc-jp' }
            @{ Encoding = 'utf-8' }
        ) {
            $bytes = [byte[]]::new(1023) + @(0xF4,0x80)
            $expected = BytesToString ($bytes | Select-Object -Last 17)

            $resultBytes = (Show-HexDump -Data $bytes -Encoding $Encoding).ListCharData.B | Select-Object -Last 17
            BytesToString $resultBytes | Should -BeExactly $expected
        }

        It 'handles single-byte incomplete sequence: <Encoding>' -ForEach @(
            @{ Encoding = 'ascii' }
            @{ Encoding = 'latin1' }
            @{ Encoding = 'utf-8' }
            @{ Encoding = 'euc-jp' }
        ) {
            $data = 'F0'
            $bytes = $data.Split().ForEach({ [byte]::Parse($_, [System.Globalization.NumberStyles]::HexNumber) })

            $resultBytes = (Show-HexDump -Data $bytes -Encoding $Encoding).ListCharData.B
            BytesToString $resultBytes | Should -BeExactly $data
        }
    }
}
