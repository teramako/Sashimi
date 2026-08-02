---
document type: cmdlet
external help file: Sashimi.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Sashimi
ms.date: 07/20/2026
PlatyPS schema version: 2024-05-01
title: Test-RawCommand
---

# Test-RawCommand

## SYNOPSIS

Executes an external command and returns true/false based the process's exit code.

## SYNTAX

### __AllParameterSets

```
Test-RawCommand [-Command] <string> [[-Arguments] <string[]>] [-Input <Object>] [-AsString]
 [-Encoding <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
- `raw?`


## DESCRIPTION

`Test-RawCommand` executes an external command and evaluates its success based on the process’s exit code.

The cmdlet returns a Boolean value:

- `true` when the process exits with code `0`
- `false` when the process exits with any non‑zero exit code

Unlike `Invoke-RawCommand`, this cmdlet does not return raw output data.
Instead, all stdout and stderr output is emitted as `InformationRecord` objects.
Each record is tagged with the originating stream (`Stdout` or `Stderr`), allowing callers to inspect diagnostic
output while still receiving a simple Boolean result.

This cmdlet is intended for scenarios where only success/failure matters—such as
conditional execution, assertions, or automation checks—while still preserving access to
the command’s textual output for logging or debugging.

## EXAMPLES

### Example 1. use in `if` statement

```powershell
if (Test-RawCommand grep pattern path/to/file) {
  # success
} else {
  throw "NotFound";
}
```

### Example 2. Getting stdout/Stderr

```powershell
if (Test-RawCommand -AsString path/to/executable -InformationVariable info) {
  # Success
  $stdout = $info.Where({$_.Tags -in "Stdout"}).MessageData.Value
  Write-Output $stdout
} else {
  $stderr = $info.Where({$_.Tags -in "Stderr"}).MessageData.Value
  throw "Failed"
}
```

## PARAMETERS

### -Arguments

Specifies the argument list passed to the external command.

Arguments are forwarded without quoting or encoding changes.
This parameter accepts remaining arguments, allowing natural PowerShell invocation.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: true
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -AsString

Returns the command output as decoded text instead of raw bytes.

When this switch is specified, the cmdlet internally decodes the captured `byte[]` stream using the encoding specified by the external process (or UTF‑8 if no encoding can be detected).
This parameter is intended for convenience when working with commands that reliably produce textual output and do not require byte‑level fidelity.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- s
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Command

Specifies the external executable to run.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Encoding

Specifies the text encoding used for:

- decoding stderr when the cmdlet emits string‑based error output
- **encoding string input from the pipeline before writing it to stdin**

When a string is piped into `Test-RawCommand`, it is converted to bytes using this encoding.  
This allows tools that expect non‑UTF8 input (e.g., Shift_JIS) to receive correctly encoded data.

Common values include `UTF-8`, `Shift_JIS`, `EUC-JP`, and other encodings supported by .NET.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- e
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Input

Provides input to the process’s standard input stream.

This parameter accepts:

- **byte** — written directly to stdin
- **byte[]** — forwarded as-is
- **string** — encoded using the encoding specified by `-Encoding`

When supplied via the pipeline, each chunk is written directly without buffering.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Byte

A single byte value can be piped to `-Input`, which is written directly to the process’s stdin.

### System.Byte[]

A byte array can be piped to `-Input`. Each array is forwarded as-is to the process’s stdin stream.

### System.String

A string can be piped to `Test-RawCommand`.
The string is encoded using the encoding specified by `-Encoding` (default: UTF‑8) and written to the process’s stdin as raw bytes.

## OUTPUTS

### System.Boolean

`$true` indicates the process's exit code is `0`, Otherwise `$false`.

## NOTES

- ScriptBlock invocation is not supported. `Test-RawCommand` always executes a native
  executable and evaluates its exit code.
- When `-AsString` is specified, stdout and stderr are decoded using the encoding
  selected by `-Encoding`. The decoded text is emitted as `InformationRecord` objects.
- Input provided via the pipeline must be either `string` or `byte[]`. Other types will
  result in a terminating error.
- This cmdlet is built on top of the same execution engine used by `Invoke-RawCommand`,
  but returns only a Boolean result instead of raw output.

## RELATED LINKS

- [Invoke-RawCommand](Invoke-RawCommand.md)
