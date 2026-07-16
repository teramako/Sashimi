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
            $result = Invoke-RawCommand $cmd $argv

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
            $result = Invoke-RawCommand iconv $argv -Input $bytes

            CompareBytes -Expected $expected -Actual $result
        }

        It 'input to the pipline as bulk' {
            $bytes = GetBytes $text $from
            $expected = GetBytes $text $to
            $result = Write-Output -NoEnumerate $bytes | Invoke-RawCommand iconv $argv

            CompareBytes -Expected $expected -Actual $result
        }

        It 'input into the pipeline by one byte' {
            $bytes = GetBytes $text $from
            $expected = GetBytes $text $to
            $result = $bytes | Invoke-RawCommand iconv $argv

            CompareBytes -Expected $expected -Actual $result
        }
    }

    Context 'Large output' {
        It 'reads 100000 lines without loss' {
            $script = Join-Path -Path $PSScriptRoot -ChildPath 'assets', 'seq100000.sh'
            $result = Invoke-RawCommand $script | ConvertTo-RawString
            $result.Count | Should -Be 100000
        }
    }

    Context 'AsString' {
        It 'echo' -ForEach @(
            @{ text = 'abc' }
        ) {
            $result = Invoke-RawCommand echo '-n' $text -AsString
            Should -BeExactly $text -ActualValue $result
        }
    }

    Context 'Cancel' {
        BeforeAll {
            $env:SashimiModulePath = Resolve-Path -RelativeBasePath $PSScriptRoot -Path ..
        }

        It 'Cancel after 1 second' {
            $job = Start-Job {
                Import-Module $env:SashimiModulePath
                Invoke-RawCommand dd if=/dev/urandom bs=8 ("count={0}" -f 10mb) -Verbose >$null
            }
            Start-Sleep -Second 1
            Stop-Job $job
            Receive-Job $job -Wait -AutoRemoveJob | Out-Null
            $job.State | Should -Be 'Stopped'
        }
    }

    Context 'Redirection' {
        BeforeAll {
            $Script:cmdPath = Join-Path -Path $PSScriptRoot -ChildPath 'assets', 'redirect_test.sh'
            # redirect_test.sh prints:
            #   StdOut to stdout
            #   StdErr to stderr
        }

        It '-Output <Name>' -ForEach @(
            @{ Name = "(Non -Output parameter)";  Arguments = @{};                    Expected = @("StdOut");           ExpectedErrors = @("StdErr") }
            @{ Name = "Stdout (same as default)"; Arguments = @{ Output = "Stdout" }; Expected = @("StdOut");           ExpectedErrors = @("StdErr") }
            @{ Name = 'Stderr (">$null 2>&1") ';  Arguments = @{ Output = "Stderr" }; Expected = @("StdErr");           ExpectedErrors = @() }
            @{ Name = 'Both ("2>&1")';            Arguments = @{ Output = "Both" };   Expected = @("StdOut", "StdErr"); ExpectedErrors = @() }
        ) {
            # -Output tests use -AsString because OutputFrom affects string routing
            # 2>&1 test omits -AsString to verify byte[] routing behavior
            $results = Invoke-RawCommand $Script:cmdPath -AsString @Arguments -ErrorVariable errors -ErrorAction SilentlyContinue

            $results | Should -Be $Expected
            $errors | Should -Be $ExpectedErrors
        }

        It 'Outputs is empty when redirection is ">$null"' {
            $results = Invoke-RawCommand $Script:cmdPath >$null -ErrorAction Ignore
            Should -BeNullOrEmpty -ActualValue $results
        }

        It 'Error outputs is empty when redirection is "2>$null"' {
            $null = Invoke-RawCommand $Script:cmdPath 2>$null -ErrorVariable errors
            Should -BeNullOrEmpty -ActualValue $errors
        }

        It 'Error outputs should be string' {
            Invoke-RawCommand $Script:cmdPath >$null -ErrorVariable errors -ErrorAction SilentlyContinue
            $errors.Count | Should -BeGreaterThan 0
            $errors[0].Exception.Message | Should -BeExactly "StdErr"
        }

        It 'Outputs from StdErr should be byte[] when redirection is "2>&1"' {
            $results = Invoke-RawCommand $Script:cmdPath -ErrorVariable errors -ErrorAction SilentlyContinue 2>&1

            $errors.Count | Should -Be 0
            $results.Count | Should -BeGreaterThan 1
            foreach ($data in $results) {
                Should -BeOfType [byte[]] -ActualValue $data
            }

            $lines = $results | ConvertTo-RawString | Sort-Object
            $lines | Should -Be @("StdErr", "StdOut")
        }
    }

    Context 'ScriptBlock' {
        It 'Proper arguments are passed: {<block>}' -ForEach @(
            @{ block = { printf 'abc' }; expected = 'abc' }
            @{ block = { printf '[''%s'']' 'a b' "`"c d`"" "e='f'" }; expected = "['a b']['`"c d`"']['e='f'']" }
        ) {
            $result = Invoke-RawCommand -AsString $block
            $result | Should -Be $expected
        }
    }
}
