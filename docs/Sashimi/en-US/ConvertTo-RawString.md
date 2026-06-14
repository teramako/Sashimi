---
document type: cmdlet
external help file: Sashimi.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Sashimi
ms.date: 06/14/2026
PlatyPS schema version: 2024-05-01
title: ConvertTo-RawString
---

# ConvertTo-RawString

## SYNOPSIS

Converts raw byte input into PowerShell strings using the specified encoding, optionally returning the entire decoded text as a single string.

## SYNTAX

### __AllParameterSets

```
ConvertTo-RawString [-InputBytes] <byte[]> [-Encoding <string>] [-Raw] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
- `b2a`


## DESCRIPTION

`ConvertTo-RawString` converts raw `byte[]` input into PowerShell strings using the specified encoding.
Unlike simple `[System.Text.Encoding]::GetString()`, this cmdlet is designed for streaming scenarios:
it accepts chunked byte arrays and reconstructs text correctly even when multi‑byte characters span
chunk boundaries.

The cmdlet emits output **one line at a time**, matching PowerShell's line‑oriented pipeline model.
This makes it suitable for decoding output from `Invoke-RawCommand`, which emits raw byte chunks
from native processes.

By default, `ConvertTo-RawString` emits one string per line, matching PowerShell’s line‑oriented pipeline model.
When the `-Raw` switch is used, the cmdlet returns the entire decoded text as a single string, preserving all newline characters.

## EXAMPLES

### Example 1

Decode Shift_JIS bytes into a PowerShell string.

```powershell
$bytes = raw some-command
$bytes | ConvertTo-RawString -Encoding Shift_JIS
```

This example decodes the raw byte output of a native command using Shift_JIS.

### Example 2

Return the entire decoded text as a single string.

```powershell
$bytes | ConvertTo-RawString -Raw
```

This example preserves all newline characters and returns the full decoded content in one object.

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
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Raw

Outputs the entire decoded text as a single string instead of splitting it into lines.
When this switch is specified, the cmdlet disables line‑oriented processing and returns the full decoded content in one object, preserving all newline characters exactly as they appear in the input stream.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- r
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

By default, one string per line is emitted.
When `-Raw` is specified, a single string containing the entire decoded content is emitted.

## NOTES

This cmdlet is designed for robust decoding of streamed binary data, including multi‑byte encodings where characters may span chunk boundaries.

## RELATED LINKS

- [ConvertFrom-RawString](ConvertFrom-RawString.md)
- [Invoke-RawCommand](Invoke-RawCommand.md)
