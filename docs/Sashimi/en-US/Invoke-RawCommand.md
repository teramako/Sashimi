---
document type: cmdlet
external help file: Sashimi.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Sashimi
ms.date: 06/12/2026
PlatyPS schema version: 2024-05-01
title: Invoke-RawCommand
---

# Invoke-RawCommand

## SYNOPSIS

Executes a native command and returns its raw byte output without any text encoding or PowerShell string conversion.

## SYNTAX

### Normal (Default)

```
Invoke-RawCommand [-Command] <string> [[-Arguments] <string[]>] [-InputBytes <byte[]>]
 [-Output <OutputType>] [<CommonParameters>]
```

### ScriptBlock

```
Invoke-RawCommand [-Script] <scriptblock> [-InputBytes <byte[]>] [-Output <OutputType>]
 [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
- `raw`

## DESCRIPTION

`Invoke-RawCommand` executes an external process and exposes its output streams as raw `byte[]` data.
Unlike standard PowerShell external command invocation, no text encoding, newline normalization, or string conversion is applied. The command’s stdout and stderr streams are emitted exactly as produced by the process.

This cmdlet is the foundation of the Sashimi module and enables precise binary‑level interaction with native tools, including those that output non‑UTF8 data, arbitrary binary payloads, or mixed encodings.

When a ScriptBlock is provided, only the first statement is analyzed and executed as a native command. This allows natural PowerShell syntax while avoiding unintended ScriptBlock execution semantics.

## EXAMPLES

### Example 1

Run a command and decode its Shift_JIS output manually.

```powershell
$bytes = raw wsl.exe --list
$bytes | ConvertTo-String -Encoding Shift_JIS
```

This example captures the raw byte output from `wsl.exe` and converts it to a string using an explicit encoding.

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
Valid values are defined by the `Sashimi.OutputType` enum (e.g., `StdOut`, `StdErr`, `Both`).
The selected streams are emitted as raw `byte[]` chunks.

```yaml
Type: Sashimi.OutputType
DefaultValue: ''
SupportsWildcards: false
Aliases: []
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

## NOTES

This cmdlet bypasses all PowerShell text processing and is intended for scenarios requiring exact byte‑level fidelity, such as binary protocols, non‑UTF8 encodings, or tools that emit mixed binary/text output.

## RELATED LINKS

- [ConvertFrom-String](ConvertFrom-String.md)
- [ConvertTo-String](ConvertTo-String.md)
