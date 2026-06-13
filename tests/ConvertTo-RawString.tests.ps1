<#
.SYNOPSIS
    Tests ConvertTo-RawString
#>
BeforeAll {
    function GetBytes([string] $Text, [string] $Encoding) {
        return [System.Text.Encoding]::GetEncoding($Encoding).GetBytes($Text);
    }
}

Describe 'ConvertTo-RawString' {
    Context 'Multi line: <encoding>' -ForEach @(
        @{ lines = @('こんにちわ’, '💩'); encoding = 'utf-8' }
        @{ lines = @('Hello’, 'こんにちわ'); encoding = 'shift_jis' }
    ) {
        It 'input as bulk' {
            $bytes = GetBytes ($lines -join "`n") $encoding
            $results = ConvertTo-RawString -InputBytes $bytes -Encoding $encoding -Verbose

            for ($i = 0; $i -lt $lines.Count; $i++) {
                Should -BeExactly $lines[$i] -ActualValue $results[$i]
            }
        }

        It 'input to the pipline as bulk' {
            $bytes = GetBytes ($lines -join "`n") $encoding
            $results = Write-Output -NoEnumerate $bytes | ConvertTo-RawString -Encoding $encoding -Verbose

            for ($i = 0; $i -lt $lines.Count; $i++) {
                Should -BeExactly $lines[$i] -ActualValue $results[$i]
            }
        }

        It 'input into the pipeline by one byte' {
            $bytes = GetBytes ($lines -join "`n") $encoding
            $results = $bytes | ConvertTo-RawString -Encoding $encoding -Verbose

            for ($i = 0; $i -lt $lines.Count; $i++) {
                Should -BeExactly $lines[$i] -ActualValue $results[$i]
            }
        }
    }
}
