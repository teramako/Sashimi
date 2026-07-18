# Sashimi

<img src="docs/img/Sashimi_512.png" height="256" align="right" alt="logo"/>

Sashimi provides raw access to external processes in PowerShell.
No encoding assumptions. No text conversion. Just pure byte streams.

PowerShell treats the output of external commands as `string`, which makes it difficult to work with binary data or arbitrary byte sequences.
Sashimi provides commands that communicate with external processes at the byte-stream level, enabling precise, streaming-based control over stdin, stdout, and stderr without any implicit encoding or text conversion.

## ✨ Commands

| Name                    | Alias  | Description |
|:------------------------|:-------|:------------|
| [Invoke-RawCommand]     | `raw`  | Execute a native command and output its StdOut/StdErr as raw `byte[]` or decoded text (`-AsString`). |
| [ConvertFrom-RawString] | `a2b`  | Convert a `string` into a byte sequence. |
| [ConvertTo-RawString]   | `b2a`  | Converts byte sequences to `string`s, one line at a time. |
| [Out-RawFile]           | `bout` | Write byte sequences into a file. |

[Invoke-RawCommand]: ./docs/Sashimi/en-US/Invoke-RawCommand.md "Invoke-RawCommand - cmdlet document"
[ConvertFrom-RawString]: ./docs/Sashimi/en-US/ConvertFrom-RawString.md "ConvertFrom-RawString - cmdlet document"
[ConvertTo-RawString]: ./docs/Sashimi/en-US/ConvertTo-RawString.md "ConvertTo-RawString - cmdlet document"
[Out-RawFile]: ./docs/Sashimi/en-US/Out-RawFile.md "Out-RawFile - cmdlet document"

## 🍣 Installation

Sashimi can be installed from the PowerShell Gallery using *PSResourceGet*,
the modern package manager included in PowerShell 7.6 and later.

### PowerShell 7.6+ (recommended)
```powershell
Install-PSResource -Name Sashimi
```

Sashimi requires PowerShell 7.6 or later.

## Examples

### 🌐 Common (Windows / Linux / macOS)

#### Get image data and convert to Sixel

```powershell
raw -s { curl https://..../image.png | img2sixel }
```

Same as:
```powershell
raw curl -- https://..../image.png | raw -s img2sixel
```

Or using `b2a` for convenience:
```powershell
raw curl -- https://..../image.png | raw img2sixel | b2a
```

#### Upload resized image via raw binary Pipeline

```powershell
raw {
  convert ./image.png -resize 32x32 |
  curl -X POST --data-binary @- https://example.dummy/upload
}
```

Same as:
```powershell
raw convert ./image.png -resize 32x32 - |
  raw curl -X POST --data-binary @- https://example.dummy/upload
```

### 🪟 Windows-specific

#### Correctly Converting `wsl.exe` command's output to a String

```powershell
raw -s -e utf-16 wsl.exe --list | select -Skip 1
```

output:
```
Ubuntu (既定値)
```

#### Correctly Converting `winget.exe` command's output to a String

```powershell
raw -s winget.exe list | ? { $_ -like "Windows *" }
```

output:
```
Windows App Runtime DDLM 3.469.1654.0-x6    MSIX\Microsoft.WinAppRuntime.DDLM.***  3.***
Windows Package Manager Source (winget) V2  MSIX\Microsoft.Winget.Source***        2026.***
Windows Subsystem for Linux Update          ARP\Machine\X64\***                    5.***
Windows Web Experience Pack                 MSIX\MicrosoftWindows.Client.***       526.***
...
Windows メモ帳                              MSIX\Microsoft.WindowsNotepad***       11.***
Windows 電卓                                MSIX\Microsoft.WindowsCalculator***    11.***
```

#### Export certificate binary directly from the Cert: drive with `bout` (`Out-RawFile`)

```powershell
Get-ChildItem Cert:\CurrentUser\Root |
  ? FriendlyName -like "VeriSign*" |
  % { $_.RawData | bout ("{0}.crt" -f $_.FriendlyName) } -End {
      Get-ChildItem *.crt
    }
```

output:
```

    Directory: D:\***

Mode                 LastWriteTime         Length Name
----                 -------------         ------ ----
-a---          2026/07/04    16:04            576 VeriSign Class 3 Public Primary CA.crt
-a---          2026/07/04    16:04            704 VeriSign Time Stamping CA.crt
-a---          2026/07/04    16:04           1239 VeriSign.crt

```

