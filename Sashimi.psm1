function Invoke-RawCommand {
    [CmdletBinding(DefaultParameterSetName = "Default")]
    [Alias("raw")]
    param(
        [Parameter(ParameterSetName = "Default", Mandatory, Position = 0)]
        [string] $Command
        ,
        [Parameter(ParameterSetName = "Default", ValueFromRemainingArguments, Position = 1)]
        [string[]] $Arguments
        ,
        [Parameter(ParameterSetName = "ScriptBlock", Mandatory, Position = 0)]
        [scriptblock] $Script
        ,
        [Parameter(ValueFromPipeline)]
        [byte[]] $InputBytes
        ,
        [Parameter()]
        [ValidateSet("Stdout", "Stderr", "All")]
        [string] $Output = "Stdout"
    )

    begin {
        $ErrorActionPreference = 'Stop'
        $stdinChunks = New-Object System.Collections.Generic.List[byte[]]
    }

    process {
        if ($InputBytes) {
            $stdinChunks.Add($InputBytes)
        }
    }

    end {
        if ($PSCmdlet.ParameterSetName -eq "ScriptBlock") {
            # 最初のステートメントのみ取得する
            $cmdAst = $Script.Ast.EndBlock.Statements.PipelineElements |
                Where-Object { $_ -is [System.Management.Automation.Language.CommandAst] } |
                Select-Object -First 1
            if (-not $cmdAst) {
                throw "raw: no command specified in ScriptBlock: $Script"
            }

            $cmd = Get-Command -CommandType Application -Name $cmdAst.GetCommandName() | Select-Object -First 1
            if (-not $cmd) {
                throw "raw: command '$cmdName' not found"
            }

            Write-Verbose "Execute the first statement in ScriptBlock: [$cmdAst]"

            $psi = [System.Diagnostics.ProcessStartInfo]::new()
            $psi.FileName = $cmd.Path

            foreach ($elem in $cmdAst.CommandElements[1..($cmdAst.CommandElements.Count - 1)]) {
                $psi.ArgumentList.Add($elem.Extent.Text)
            }
        } else {
            # if ($Arguments.Count -eq 0) {
            #     throw "raw: no command specified:"
            # }

            Write-Verbose "Execute: $Command"

            $exe = $Command

            # ProcessStartInfo を構築
            $psi = [System.Diagnostics.ProcessStartInfo]::new()
            $psi.FileName = $exe

            # 引数は AddArgument で安全に渡す
            foreach ($a in $Arguments) {
                $psi.ArgumentList.Add($a)
            }
        }

        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError  = $true
        $psi.RedirectStandardInput  = $true


        $proc = [System.Diagnostics.Process]::Start($psi);
        if ($stdinChunks.Count -gt 0) {
            foreach ($chunk in $stdinChunks) {
                $proc.StandardInput.BaseStream.Write($chunk, 0, $chunk.Length)
            }
        }
        # stdin を閉じる
        $proc.StandardInput.Close()

        # stdout/stderr を読み取る
        $stdout = $proc.StandardOutput.BaseStream
        $stderr = $proc.StandardError.BaseStream

        $buffer = New-Object byte[] 4096

        if ($Output -eq "Stdout" -or $Output -eq "All") {
            while (($read = $stdout.Read($buffer, 0, $buffer.Length)) -gt 0) {
                Write-Output -NoEnumerate ($buffer[0..($read-1)] -as [byte[]])
            }
        }

        if ($Output -eq "Stderr" -or $Output -eq "All") {
            while (($read = $stderr.Read($buffer, 0, $buffer.Length)) -gt 0) {
                Write-Output -NoEnumerate ($buffer[0..($read-1)] -as [byte[]])
            }
        }

        $proc.WaitForExit()

        # exit code を $global:LASTEXITCODE に反映
        $global:LASTEXITCODE = $proc.ExitCode
    }
}
