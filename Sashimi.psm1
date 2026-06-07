function Invoke-RawCommand {
    [CmdletBinding(DefaultParameterSetName = "Default")]
    [Alias("raw")]
    param(
        [Parameter(ParameterSetName = "Default", Mandatory, ValueFromRemainingArguments, Position = 0)]
        [string[]] $Arguments
        ,
        [Parameter(ParameterSetName = "ScriptBlock", Mandatory, Position = 0)]
        [scriptblock] $Script
    )

    if ($PSCmdlet.ParameterSetName -eq "ScriptBlock") {
        # 最初のステートメントのみ取得する
        $cmdAst = $Script.Ast.EndBlock.Statements |
            Where-Object { $_ -is [System.Management.Automation.Language.CommandAst] } |
            Select-Object -First 1
        if (-not $cmdAst) {
            throw "raw: no command specified"
        }

        $cmd = Get-Command -CommandType Application -Name $cmdAst.GetCommandName() | Select-Object -First 1
        if (-not $cmd) {
            throw "raw: command '$cmdName' not found"
        }

        $psi = [System.Diagnostics.ProcessStartInfo]::new()
        $psi.FileName = $cmd.Path

        foreach ($elem in $cmdAst.CommandElements[1..($cmdAst.CommandElements.Count - 1)]) {
            $psi.ArgumentList.Add($elem.Extent.Text)
        }
    } else {
        if ($Arguments.Count -eq 0) {
            throw "raw: no command specified"
        }

        $exe = $Arguments[0]
        $argList = $Arguments[1..($Arguments.Count - 1)]

        # ProcessStartInfo を構築
        $psi = [System.Diagnostics.ProcessStartInfo]::new()
        $psi.FileName = $exe

        # 引数は AddArgument で安全に渡す
        foreach ($a in $argList) {
            $psi.ArgumentList.Add($a)
        }
    }

    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.RedirectStandardInput  = $true


    $proc = [System.Diagnostics.Process]::new()
    $proc.StartInfo = $psi
    $proc.Start() | Out-Null

    # stdin を閉じる（パイプ入力対応は後で）
    $proc.StandardInput.Close()

    # stdout/stderr を読み取る
    $stdout = $proc.StandardOutput.BaseStream
    $stderr = $proc.StandardError.BaseStream

    $buffer = New-Object byte[] 4096

    # stdout を byte[] チャンクとして流す
    while (($read = $stdout.Read($buffer, 0, $buffer.Length)) -gt 0) {
        Write-Output ($buffer[0..($read-1)] -as [byte[]])
    }

    # stderr は最後にまとめて出す（用途に応じて変えられる）
    $errBuf = New-Object System.IO.MemoryStream
    while (($read = $stderr.Read($buffer, 0, $buffer.Length)) -gt 0) {
        $errBuf.Write($buffer, 0, $read)
    }

    $proc.WaitForExit()

    # exit code を $global:LASTEXITCODE に反映
    $global:LASTEXITCODE = $proc.ExitCode

    # stderr があれば書き出す（必要なら非表示にもできる）
    if ($errBuf.Length -gt 0) {
        Write-Error -Message ("raw stderr: " + [System.Text.Encoding]::UTF8.GetString($errBuf.ToArray()))
    }
}
