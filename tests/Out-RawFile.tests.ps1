<#
.SYNOPSIS
    Tests Out-RawFile
#>

Describe 'Out-RawFile' {
    Context 'Basic' {
        It 'Small output' {
            $filePath = "TestDrive:/small.out"
            $text = 'abc'
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)

            ConvertFrom-RawString 'abc' | Out-RawFile -Path $filePath

            $file = Get-Item $filePath
            $file.Exists | Should -BeTrue
            $file.Size | Should -BeExactly $bytes.Length
        }

        It 'Overwrite' {
            $filePath = "TestDrive:/overwrite.out"
            $text1 = 'abc'
            $text2 = 'def'

            ConvertFrom-RawString $text1 | Out-RawFile -Path $filePath
            ConvertFrom-RawString $text2 | Out-RawFile -Path $filePath

            Get-Content $filePath | Should -Be $text2
        }

        It 'Append' {
            $filePath = "TestDrive:/append.out"
            $text1 = 'abc'
            $text2 = 'def'

            ConvertFrom-RawString $text1 | Out-RawFile -Path $filePath
            ConvertFrom-RawString $text2 | Out-RawFile -Path $filePath -Append

            Get-Content $filePath | Should -Be ($text1 + $text2)
        }

        It 'PassThru' {
            $filePath = "TestDrive:/passthru.out"
            $text1 = 'abc'
            $text2 = 'def'

            $result1 = ConvertFrom-RawString $text1 | Out-RawFile -Path $filePath -PassThru
            $result2 = ConvertFrom-RawString $text2 | Out-RawFile -Path $filePath -Append -PassThru

            $result1 | ConvertTo-RawString | Should -Be $text1
            $result2 | ConvertTo-RawString | Should -Be $text2
        }

        It 'Large output' {
            $filePath = "TestDrive:/large.out"
            $script = Join-Path -Path $PSScriptRoot -ChildPath 'assets', 'seq100000.sh'
            Invoke-RawCommand $script | Out-RawFile $filePath

            Get-Content $filePath | Measure-Object -Line | Select-Object -ExpandProperty Lines | Should -Be 100000
        }
    }
}
