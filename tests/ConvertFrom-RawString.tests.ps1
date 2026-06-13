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
}
