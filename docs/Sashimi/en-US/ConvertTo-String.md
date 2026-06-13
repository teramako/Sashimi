---
document type: cmdlet
external help file: Sashimi.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Sashimi
ms.date: 06/13/2026
PlatyPS schema version: 2024-05-01
title: ConvertTo-String
---

# ConvertTo-String

## SYNOPSIS

Converts raw byte input into PowerShell strings using the specified text encoding.

## SYNTAX

### __AllParameterSets

```
ConvertTo-String -InputBytes <byte[]> [-Encoding <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
- `b2a`


## DESCRIPTION

`ConvertTo-String` converts raw `byte[]` input into PowerShell strings using the specified encoding.
Unlike simple `[System.Text.Encoding]::GetString()`, this cmdlet is designed for streaming scenarios:
it accepts chunked byte arrays and reconstructs text correctly even when multi‑byte characters span
chunk boundaries.

The cmdlet emits output **one line at a time**, matching PowerShell's line‑oriented pipeline model.
This makes it suitable for decoding output from `Invoke-RawCommand`, which emits raw byte chunks
from native processes.

## EXAMPLES

### Example 1

Decode Shift_JIS bytes into a PowerShell string.

```powershell
$bytes = raw some-command
$bytes | ConvertTo-String -Encoding Shift_JIS
```

This example decodes the raw byte output of a native command using Shift_JIS.

## PARAMETERS

### -Encoding

Specifies the text encoding used to decode the input bytes.
Any encoding name accepted by `[System.Text.Encoding]::GetEncoding()` is valid.
If omitted, UTF‑8 is used.

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

### -InputBytes

Specifies the byte array to decode.
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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Byte

A single byte can be piped to this cmdlet and will be buffered as part of the decoding stream.

### System.Byte[]

A byte array can be piped to this cmdlet. Each chunk is decoded as part of the continuous stream.

## OUTPUTS

### System.String

The cmdlet outputs decoded text as PowerShell strings.
Line‑based output is produced when the underlying stream reader encounters newline sequences.

## NOTES

This cmdlet is designed for robust decoding of streamed binary data, including multi‑byte encodings where characters may span chunk boundaries.

## RELATED LINKS

- [ConvertFrom-String](ConvertFrom-String.md)
- [Invoke-RawCommand](Invoke-RawCommand.md)
