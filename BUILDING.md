# Building

The project references assemblies supplied by the installed game. The game
installation path is not stored in `ADOFAI_DX.csproj`.

1. Copy `ADOFAI_DX/Directory.Build.props.example` to
   `ADOFAI_DX/Directory.Build.props`.
2. Set `AdofaiGamePath` to the folder containing
   `A Dance of Fire and Ice.exe`.
3. Open `ADOFAI_DX.slnx` in Visual Studio, or run:

```powershell
dotnet build ADOFAI_DX/ADOFAI_DX/ADOFAI_DX.csproj -c Release
```

`Directory.Build.props` is ignored by Git so personal absolute paths are not
published.

Alternatively, set the `ADOFAI_GAME_PATH` environment variable before
building.
