---
document type: cmdlet
external help file: Sashimi.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Sashimi
ms.date: 06/13/2026
PlatyPS schema version: 2024-05-01
title: Out-RawFile
---

# Out-RawFile

## SYNOPSIS

Writes raw byte input to a file without any text encoding or transformation.

## SYNTAX

### Default (Default)

```
Out-RawFile [-Path] <string> -InputBytes <byte[]> [-Append] [<CommonParameters>]
```

### PassThru

```
Out-RawFile [-Path] <string> -InputBytes <byte[]> -PassThru [-Append]
```

## ALIASES

`rawout`

## DESCRIPTION

`Out-RawFile` writes raw `byte[]` data directly to a file.
No text encoding, newline normalization, or PowerShell string conversion is applied.
This cmdlet is the counterpart to `Get-Content -AsByteStream` and is designed for binary‑safe pipelines, especially when used with `Invoke-RawCommand`, `ConvertFrom-String`, and `ConvertTo-String`.

When `-Append` is specified, data is added to the end of the file.
When `-PassThru` is used, the written bytes are emitted back to the pipeline, enabling tee‑like scenarios.

## EXAMPLES

### Example 1

Write raw bytes to a file.

```powershell
raw some-command | Out-RawFile output.bin
```

This example captures the raw byte output of a native command and writes it directly to `output.bin`.

## PARAMETERS

### -Append

Appends the incoming bytes to the end of the file instead of overwriting it.
If the file does not exist, it is created.

```yaml
Type: System.Management.Automation.SwitchParameter
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

### -InputBytes

Specifies the raw byte array to write to the file.
This parameter accepts pipeline input and supports chunked streaming.

```yaml
Type: System.Byte[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PassThru

Outputs the written bytes back to the pipeline.
This is useful for chaining additional processing or for tee‑style workflows.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: PassThru
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Path

Specifies the file path to write to.
Relative paths are resolved using PowerShell’s path resolution rules.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Byte

A single byte can be piped to this cmdlet and will be written to the file.

### System.Byte[]

A byte array can be piped to this cmdlet. Each chunk is written directly to the file.

## OUTPUTS

### System.Void

By default, this cmdlet produces no output.

### System.Byte[]

When `-PassThru` is specified, the written bytes are emitted back to the pipeline.

## NOTES

This cmdlet provides the missing counterpart to `Get-Content -AsByteStream`, enabling fully binary‑safe pipelines in PowerShell.
It is optimized for streaming scenarios and large binary data.

## RELATED LINKS

* [Invoke-RawCommand](Invoke-RawCommand.md)
* [ConvertFrom-String](ConvertFrom-String.md)
* [ConvertTo-String](ConvertTo-String.md)

