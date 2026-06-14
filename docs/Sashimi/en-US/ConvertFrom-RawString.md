---
document type: cmdlet
external help file: Sashimi.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Sashimi
ms.date: 06/14/2026
PlatyPS schema version: 2024-05-01
title: ConvertFrom-RawString
---

# ConvertFrom-RawString

## SYNOPSIS

Converts one or more PowerShell strings into byte arrays using the specified encoding, optionally inserting a delimiter between pipeline inputs.

## SYNTAX

### __AllParameterSets

```
ConvertFrom-RawString [-InputString] <string> [-Encoding <string>] [-Delimiter <string>]
 [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
- `a2b`


## DESCRIPTION

`ConvertFrom-RawString` converts a PowerShell `string` into a `byte[]` using the specified encoding.
This cmdlet is the counterpart of `ConvertTo-RawString` and is typically used when preparing text for binary‑level pipelines, such as feeding encoded data into `Invoke-RawCommand`.

PowerShell normally converts strings to bytes using UTF‑16, but many native tools expect UTF‑8, Shift_JIS, or other encodings. This cmdlet provides explicit control over the encoding used.

When multiple strings are provided through the pipeline, `ConvertFrom-RawString` concatenates them by inserting the specified delimiter (if any) before encoding.
This allows constructing structured byte sequences such as newline‑separated text or custom‑delimited records.

## EXAMPLES

### Example 1

Convert a string to Shift_JIS bytes.

```powershell
"こんにちは" | ConvertFrom-RawString -Encoding Shift_JIS
```

This example produces a `byte[]` representing the Shift_JIS encoding of the input string.

### Example 2

Insert a newline delimiter between multiple input strings.

```powershell
"line1","line2","line3" | ConvertFrom-RawString -Delimiter "`n"
```

This produces a single `byte[]` containing the three lines separated by LF.

## PARAMETERS

### -Delimiter

Specifies a delimiter string to insert before each input string after the first one.
When multiple strings are provided through the pipeline, the delimiter is encoded using the selected encoding and written before each subsequent string.
This is useful when constructing multi‑line or structured byte sequences, such as inserting newline characters or custom separators between records.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- d
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

One or more strings can be piped to this cmdlet.
Each string is encoded and emitted as a byte[], with the delimiter inserted before subsequent strings when specified.

## OUTPUTS

### System.Byte[]

The cmdlet outputs one or more byte[] objects representing the encoded input strings, with delimiters inserted when applicable.

## NOTES

This cmdlet is intended for precise control of text‑to‑binary conversion, especially when interacting with native commands that require specific encodings.

## RELATED LINKS

- [ConvertTo-RawString](ConvertTo-RawString.md)
- [Invoke-RawCommand](Invoke-RawCommand.md)
