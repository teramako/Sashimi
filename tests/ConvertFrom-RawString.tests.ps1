<#
.SYNOPSIS
    Tests ConvertFrom-RawString
#>
BeforeAll {
    function ToByteSeq([byte[]] $Bytes) {
        return $Bytes.ForEach({ $_.ToString("X2") }) -join ' '
    }

    function CompareBytes([byte[]] $Expected, [byte[]] $Actual) {
        Should -BeExactly (ToByteSeq $Expected) -ActualValue (ToByteSeq $Actual)
    }
}
Describe 'ConvertFrom-RawString' {
    Context 'Basic' -ForEach @(
        @{ text = 'うんこ' }
    ) {
        It 'Non-Encoding (default: utf-8)' {
            $result = ConvertFrom-RawString -InputString $text
            $expected = [System.Text.Encoding]::UTF8.GetBytes($text);

            CompareBytes -Expected $expected -Actual $result
        }
        It 'UTF-8 Encoding' {
            $result = ConvertFrom-RawString -InputString $text -Encoding utf-8
            $expected = [System.Text.Encoding]::UTF8.GetBytes($text);

            CompareBytes -Expected $expected -Actual $result
        }
        It 'Shift_JIS Encoding' {
            $result = ConvertFrom-RawString -InputString $text -Encoding shift_jis
            $expected = [System.Text.Encoding]::GetEncoding('shift_jis').GetBytes($text);

            CompareBytes -Expected $expected -Actual $result
        }
    }
    Context 'Input of an array of strings' {
        BeforeAll {
            $script:testItems = @("a", "b")
        }

        It 'Non Delimiter' {
            $expected = [System.Text.Encoding]::UTF8.GetBytes(($testItems -join ""))

            $result = $testItems | ConvertFrom-RawString | ForEach-Object { $_ }

            CompareBytes -Expected $expected -Actual $result
        }

        It 'Delimiter <name>' -ForEach @(
            @{ name = 'LF'; delimiter = "`n" }
            @{ name = 'Comma'; delimiter = ',' }
        ) {
            $expected = [System.Text.Encoding]::UTF8.GetBytes(($testItems -join $delimiter))

            $result = $testItems | ConvertFrom-RawString -Delimiter $delimiter | ForEach-Object { $_ }

            CompareBytes -Expected $expected -Actual $result
        }
    }
}
