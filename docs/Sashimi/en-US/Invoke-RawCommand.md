---
document type: cmdlet
external help file: Sashimi.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Sashimi
ms.date: 07/14/2026
PlatyPS schema version: 2024-05-01
title: Invoke-RawCommand
---

# Invoke-RawCommand

## SYNOPSIS

Executes a native command and returns its output as raw bytes or decoded text.

## SYNTAX

### Normal (Default)

```
Invoke-RawCommand [-Command] <string> [[-Arguments] <string[]>] [-InputBytes <byte[]>]
 [-Output <OutputFrom>] [-AsString] [-Encoding <string>] [<CommonParameters>]
```

### ScriptBlock

```
Invoke-RawCommand [-Script] <scriptblock> [-InputBytes <byte[]>] [-Output <OutputFrom>] [-AsString]
 [-Encoding <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
- `raw`


## DESCRIPTION

`Invoke-RawCommand` executes an external process and exposes its output streams as raw `byte[]` data.
Unlike standard PowerShell external command invocation, no text encoding, newline normalization, or string conversion is applied. The command’s stdout and stderr streams are emitted exactly as produced by the process.

This cmdlet is the foundation of the Sashimi module and enables precise binary‑level interaction with native tools, including those that output non‑UTF8 data, arbitrary binary payloads, or mixed encodings.

When a ScriptBlock is provided, only the first statement is analyzed and executed as a native command. This allows natural PowerShell syntax while avoiding unintended ScriptBlock execution semantics.

`Invoke-RawCommand` normally emits raw `byte[]` data with no text encoding or string conversion.
When the `-AsString` switch is used, the cmdlet decodes the output into a PowerShell string for convenience.

When `-AsString` is not used, stdout is emitted as raw `byte[]` chunks.  
Stderr is also captured as raw bytes, but when not selecting raw output modes,  
stderr is decoded using the encoding specified by `-Encoding` and emitted as `InformationRecord` messages.
This allows stderr to appear as readable text in the PowerShell pipeline while preserving byte‑level fidelity for stdout.

The `-Encoding` parameter controls how stderr is decoded (default: UTF‑8).  
This is useful for tools that emit non‑UTF8 error messages such as Shift_JIS or other legacy encodings.

## EXAMPLES

### Example 1

Run a command and decode its UTF-16 output manually.

```powershell
$bytes = raw wsl.exe --list
$bytes | ConvertTo-RawString -Encoding UTF-16
```

This example captures the raw byte output from `wsl.exe` and converts it to a string using an explicit encoding.

### Example 2

Convert Shift_JIS bytes using `iconv` and return the result as a decoded string.

```powershell
$bytes = ConvertFrom-RawString テスト -Encoding shift_jis
$string = $bytes | Invoke-RawCommand iconv '-f' shift_jis -AsString
# same as:
#        `$bytes | Invoke-RawCommand iconv '-f' shift_jis | ConvertTo-RawString`
```

This example returns the command output as a decoded string without requiring `ConvertTo-RawString`.

## PARAMETERS

### -Arguments

Specifies the argument list passed to the native command.
Arguments are forwarded without quoting or encoding changes.
This parameter accepts remaining arguments, allowing natural PowerShell invocation.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Normal
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

`-AsString` cannot be used together with `ConvertTo-RawString`, since decoding is performed inside the cmdlet.

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

Specifies the native executable to run.
This parameter is required when using the Normal parameter set.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Normal
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

Specifies the text encoding used to decode stderr when the cmdlet emits string‑based error output.
Stderr byte chunks are decoded using this encoding and written as `InformationRecord` messages unless raw output is selected.

This parameter does not affect stdout decoding when `-AsString` is used;  
stdout is decoded using the encoding detected from the process output or UTF‑8 when no encoding can be determined.

Common values include `UTF-8`, `Shift_JIS`, `EUC-JP`, and other encodings supported by .NET.

```yaml
Type: System.String
DefaultValue: 'UTF-8'
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

### -InputBytes

Provides raw byte input to the process’s standard input stream.
When supplied via the pipeline, each `byte[]` chunk is written directly without buffering or encoding.

```yaml
Type: System.Byte[]
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

### -Output

Specifies which output streams to emit.
Valid values are defined by the `Sashimi.OutputFrom` enum (e.g., `Stdout`, `Stderr`, `Both`).
The selected streams are emitted as raw `byte[]` chunks.

```yaml
Type: Sashimi.OutputFrom
DefaultValue: ''
SupportsWildcards: false
Aliases:
- o
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

### -Script

Specifies a ScriptBlock whose first statement is executed as a native command.
This enables natural syntax such as:

```powershell
raw { git status }
```

Only the first statement is used; the ScriptBlock is not executed as PowerShell code.

```yaml
Type: System.Management.Automation.ScriptBlock
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ScriptBlock
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
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

A single byte value can be piped to `-InputBytes`, which is written directly to the process’s stdin.

### System.Byte[]

A byte array can be piped to `-InputBytes`. Each array is forwarded as-is to the process’s stdin stream.

## OUTPUTS

### System.Byte[]

The cmdlet outputs raw bytes from the selected output streams (`StdOut`, `StdErr`, or both).
Each emitted object is a `byte[]` chunk representing data read from the process.

### System.String

Returned when `-AsString` is specified.
The cmdlet decodes the raw output stream into a PowerShell string using the detected or default encoding.

## NOTES

This cmdlet bypasses all PowerShell text processing and is intended for scenarios requiring exact byte‑level fidelity, such as binary protocols, non‑UTF8 encodings, or tools that emit mixed binary/text output.

## RELATED LINKS

- [ConvertFrom-RawString](ConvertFrom-RawString.md)
- [ConvertTo-RawString](ConvertTo-RawString.md)
