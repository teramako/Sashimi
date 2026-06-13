---
document type: cmdlet
external help file: Sashimi.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Sashimi
ms.date: 06/12/2026
PlatyPS schema version: 2024-05-01
title: ConvertFrom-RawString
---

# ConvertFrom-RawString

## SYNOPSIS

Converts a PowerShell string into a byte array using the specified text encoding.

## SYNTAX

### __AllParameterSets

```
ConvertFrom-RawString [-InputString] <string> [-Encoding <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
- `a2b`


## DESCRIPTION

`ConvertFrom-RawString` converts a PowerShell `string` into a `byte[]` using the specified encoding.
This cmdlet is the counterpart of `ConvertTo-RawString` and is typically used when preparing text for binary‑level pipelines, such as feeding encoded data into `Invoke-RawCommand`.

PowerShell normally converts strings to bytes using UTF‑16, but many native tools expect UTF‑8, Shift_JIS, or other encodings. This cmdlet provides explicit control over the encoding used.

## EXAMPLES

### Example 1

Convert a string to Shift_JIS bytes.

```powershell
"こんにちは" | ConvertFrom-RawString -Encoding Shift_JIS
```

This example produces a `byte[]` representing the Shift_JIS encoding of the input string.

## PARAMETERS

### -Encoding

Specifies the text encoding used to convert the input string into bytes.
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

### -InputString

Specifies the string to convert.
This parameter accepts pipeline input.

```yaml
Type: System.String
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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String

A string can be piped to this cmdlet and will be converted to a byte array.

## OUTPUTS

### System.Byte[]

The cmdlet outputs a `byte[]` representing the encoded form of the input string.

## NOTES

This cmdlet is intended for precise control of text‑to‑binary conversion, especially when interacting with native commands that require specific encodings.

## RELATED LINKS

- [ConvertTo-RawString](ConvertTo-RawString.md)
- [Invoke-RawCommand](Invoke-RawCommand.md)
