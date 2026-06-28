# Sashimi

<img src="docs/img/Sashimi_512.png" height="256" align="right" alt="logo"/>

Sashimi provides raw access to external processes in PowerShell.
No encoding assumptions. No text conversion. Just pure byte streams.

PowerShell treats the output of external commands as `string`, which makes it difficult to work with binary data or arbitrary byte sequences.
Sashimi provides commands that communicate with external processes at the byte-stream level, enabling precise, streaming-based control over stdin, stdout, and stderr without any implicit encoding or text conversion.

## ✨ Commands

| Name                    | Alias  | Description |
|:------------------------|:-------|:------------|
| `Invoke-RawCommand`     | `raw`  | Execute a native command and output its StdOut/StdErr as raw `byte[]` or decoded text (`-AsString`). |
| `ConvertFrom-RawString` | `a2b`  | Convert a `string` into a byte sequence. |
| `ConvertTo-RawString`   | `b2a`  | Converts byte sequences to `string`s, one line at a time. |
| `Out-RawFile`           | `bout` | Write byte sequences into a file. |

## 🍣 Installation

Sashimi can be installed from the PowerShell Gallery using *PSResourceGet*,
the modern package manager included in PowerShell 7.6 and later.

### PowerShell 7.6+ (recommended)
```powershell
Install-PSResource -Name Sashimi
```

Sashimi requires PowerShell 7.6 or later.

## 🗺️ Roadmap

### Version 1.0 — Core foundation
- [x] Stabilize `RawProcessRunner` API (async I/O, exit code, cancellation)
- [x] Finalize `raw` command UX and parameter behavior
- [x] Support ScriptBlock with “first statement only” execution rule
- [x] Implement `ConvertTo-RawString` / `ConvertFrom-RawString` for encoding transforms
- [x] Ensure consistent byte[] pipeline behavior across platforms
- [x] Document core usage and module structure

### Version 2.0 — Pipeline integration
- [ ] Introduce internal `rawInternal` command (not exported)
- [ ] Parse ScriptBlock pipeline and map to PowerShell pipeline
- [ ] Enable: `raw { cmd1 | cmd2 }` → `rawInternal cmd1 | rawInternal cmd2`
- [ ] Stream stdin/stdout between external processes naturally
- [ ] Improve error handling and diagnostics for pipeline mode

### Version 3.0 — HexDump integration
- [ ] Integrate HexDump as an official Sashimi component
- [ ] Provide unified byte[] visualization (`Show-HexDump`)
- [ ] Enable natural chaining: `raw { ... } | Show-HexDump`
- [ ] Consolidate documentation and examples for the full I/O workflow

