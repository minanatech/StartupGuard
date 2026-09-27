# Startup Guard

Startup Guard is a small portable startup manager for Windows. It shows startup entries from the Windows Registry and lets you disable or restore selected items.

## Features

- Lists startup apps from Current User and All Users Registry locations
- Disables a startup item by moving it to a Startup Guard backup key
- Restores disabled items from the backup key
- Opens the file location for a startup command when the path can be detected
- Portable single EXE

## Requirements

- Windows 7 SP1, Windows 10, or Windows 11
- .NET Framework 4.8 recommended
- Administrator rights are required to change All Users startup entries

## Notes

Startup Guard only manages Registry startup entries under:

- `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- `HKLM\Software\Microsoft\Windows\CurrentVersion\Run`

It does not remove programs, edit scheduled tasks, or change Windows services.

## Build

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ /out:StartupGuard.exe Program.cs
```

## License

MIT License.
