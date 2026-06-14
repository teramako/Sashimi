<#
.SYNOPSIS
    Tests ConvertTo-RawString
#>

Describe 'ConvertTo-RawString' {
    Context 'Multi line: <encoding>' -ForEach @(
        @{ lines = @('こんにちは’, '💩'); encoding = 'utf-8' }
        @{ lines = @('Hello’, 'こんにちは'); encoding = 'shift_jis' }
    ) {
        It 'input as bulk (<name>)' -ForEach @(
            @{ name = "CR"; delimiter = "`r" },
            @{ name = "LF"; delimiter = "`n" },
            @{ name = "CRLF"; delimiter = "`r`n" }
        ) {
            $bytes = ConvertFrom-RawString ($lines -join $delimiter) -Encoding $encoding
            $results = ConvertTo-RawString -InputBytes $bytes -Encoding $encoding

            for ($i = 0; $i -lt $lines.Count; $i++) {
                Should -BeExactly $lines[$i] -ActualValue $results[$i]
            }
        }

        It 'input to the pipline as bulk (<name>)' -ForEach @(
            @{ name = "CR"; delimiter = "`r" },
            @{ name = "LF"; delimiter = "`n" },
            @{ name = "CRLF"; delimiter = "`r`n" }
        ) {
            $bytes = ConvertFrom-RawString ($lines -join $delimiter) -Encoding $encoding
            $results = Write-Output -NoEnumerate $bytes | ConvertTo-RawString -Encoding $encoding

            for ($i = 0; $i -lt $lines.Count; $i++) {
                Should -BeExactly $lines[$i] -ActualValue $results[$i]
            }
        }

        It 'input into the pipeline by one byte (<name>)' -ForEach @(
            @{ name = "CR"; delimiter = "`r" },
            @{ name = "LF"; delimiter = "`n" },
            @{ name = "CRLF"; delimiter = "`r`n" }
        ) {
            $bytes = ConvertFrom-RawString ($lines -join $delimiter) -Encoding $encoding
            $results = $bytes | ConvertTo-RawString -Encoding $encoding

            for ($i = 0; $i -lt $lines.Count; $i++) {
                Should -BeExactly $lines[$i] -ActualValue $results[$i]
            }
        }

        It '-Raw mode (<name>)' -ForEach @(
            @{ name = "CR"; delimiter = "`r" },
            @{ name = "LF"; delimiter = "`n" },
            @{ name = "CRLF"; delimiter = "`r`n" }
        ) {
            $expected = $lines -join $delimiter
            $bytes = ConvertFrom-RawString $expected -Encoding $encoding
            $bytes | ConvertTo-RawString -Encoding $encoding -Raw | Should -BeExactly $expected
        }
    }
}
