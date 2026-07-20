---
document type: module
Help Version: 1.0.0.0
HelpInfoUri: 
Locale: en-US
Module Guid: 45b865bc-8ff9-4932-899f-26f7554472ff
Module Name: Sashimi
ms.date: 07/20/2026
PlatyPS schema version: 2024-05-01
title: Sashimi Module
---

# Sashimi Module

## Description

Raw process I/O for PowerShell — execute external commands and handle stdin/stdout/stderr as byte streams.

## Sashimi

### [ConvertFrom-RawString](ConvertFrom-RawString.md)

Converts one or more PowerShell strings into byte arrays using the specified encoding, optionally inserting a delimiter between pipeline inputs.

### [ConvertTo-RawString](ConvertTo-RawString.md)

Converts raw byte input into PowerShell strings using the specified encoding, optionally returning the entire decoded text as a single string.

### [Invoke-RawCommand](Invoke-RawCommand.md)

Executes a native command and returns its output as raw bytes or decoded text.

### [Out-RawFile](Out-RawFile.md)

Writes raw byte input to a file without any text encoding or transformation.

### [Test-RawCommand](Test-RawCommand.md)

Executes an external command and returns true/false based the process's exit code.

