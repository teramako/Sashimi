<#
.SYNOPSIS
    Tests Invoke-RawCommand
#>
BeforeAll {
    function GetBytes([string] $Text, [string] $Encoding) {
        return [System.Text.Encoding]::GetEncoding($Encoding).GetBytes($Text);
    }
    function ToByteSeq([byte[]] $Bytes) {
        return $Bytes.ForEach({ $_.ToString("X2") }) -join ' '
    }

    function CompareBytes([byte[]] $Expected, [byte[]] $Actual) {
        Should -BeExactly (ToByteSeq $Expected) -ActualValue (ToByteSeq $Actual)
    }
}

Describe 'Invoke-RawCommand' {
    Context 'Basic' {
        It '<cmd> <argv>' -ForEach @(
            @{ cmd = 'echo'; argv = 'abc';      expected = [byte[]]@(0x61, 0x62, 0x63, 0x0A) },
            @{ cmd = 'echo'; argv = '-n','abc'; expected = [byte[]]@(0x61, 0x62, 0x63) }
        ) {
            $result = Invoke-RawCommand $cmd $argv -Verbose

            CompareBytes -Expected $expected -Actual $result
        }

        It 'LASTEXITCODE should be set to <expect> (Command: "<cmd>")' -ForEach @(
            @{ cmd = 'true';  expect = 0 },
            @{ cmd = 'false'; expect = 1 }
        ) {
            $null = Invoke-RawCommand $cmd
            Should -BeExactly $expect -ActualValue $LASTEXITCODE
        }
    }

    Context 'Input Stdin: "<text>" (from:<from> to:<to>)' -ForEach @(
        @{ text = 'うんこ’; argv = '-f','utf8','-t','sjis'; from = 'utf-8'; to = 'shift_jis' },
        @{ text = 'うんこ’; argv = '-f','sjis','-t','utf8'; from = 'shift_jis'; to = 'utf-8' }
    ) {
        It 'input as bulk' {
            $bytes = GetBytes $text $from
            $expected = GetBytes $text $to
            $result = Invoke-RawCommand iconv $argv -InputBytes $bytes -Verbose

            CompareBytes -Expected $expected -Actual $result
        }

        It 'input to the pipline as bulk' {
            $bytes = GetBytes $text $from
            $expected = GetBytes $text $to
            $result = Write-Output -NoEnumerate $bytes | Invoke-RawCommand iconv $argv -Verbose

            CompareBytes -Expected $expected -Actual $result
        }

        It 'input into the pipeline by one byte' {
            $bytes = GetBytes $text $from
            $expected = GetBytes $text $to
            $result = $bytes | Invoke-RawCommand iconv $argv -Verbose

            CompareBytes -Expected $expected -Actual $result
        }
    }

    Context 'Large output' {
        It 'reads 100000 lines without loss' {
            $script = Join-Path -Path $PSScriptRoot -ChildPath 'assets', 'seq100000.sh'
            $result = Invoke-RawCommand $script -Verbose | ConvertTo-RawString
            $result.Count | Should -Be 100000
        }
    }
}
