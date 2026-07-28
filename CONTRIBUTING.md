# Contributing to Fusebox

Thanks for taking the time to contribute.

## Development

Development requires the .NET 10 SDK. Windows is the primary platform; macOS is a
validation target.

```shell
dotnet restore Fusebox.sln
dotnet run --project src/Fusebox/Fusebox.csproj
```

## Before opening a pull request

1. Open an issue first for substantial changes so the approach can be discussed.
2. Keep each pull request focused on one change.
3. Build the solution in Release configuration:

   ```shell
   dotnet restore Fusebox.sln
   dotnet build Fusebox.sln --configuration Release --no-restore -p:TreatWarningsAsErrors=true
   ```

4. Add or update tests when the project area has automated coverage.
5. Update documentation when behavior or configuration changes.

## Build for distribution

The following commands create self-contained, single-file Release builds. Users do
not need to install .NET separately.

### Windows (64-bit)

```shell
dotnet publish src/Fusebox/Fusebox.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output artifacts/publish/win-x64
```

The distributable executable is `artifacts/publish/win-x64/Fusebox.exe`.

### macOS

```shell
bash scripts/package-macos.sh
```

This publishes both architectures and creates Finder-ready application bundles:

- `artifacts/publish/osx-arm64/Fusebox.app` for Apple Silicon
- `artifacts/publish/osx-x64/Fusebox.app` for Intel Macs

Pass `osx-arm64` or `osx-x64` to package only one architecture. Set
`APP_VERSION`, for example
`APP_VERSION=1.2.0 bash scripts/package-macos.sh`, to write a release version
into the bundle metadata.

The script converts the Fusebox artwork to a macOS `.icns` file and stores it
with the executable in the standard `.app` directory structure. The bundles are
not signed or notarized; those steps are independent of the application icon.

Run the Windows publish command on Windows and the macOS packaging script on
macOS.

## Architecture

See [Architecture](docs/ARCHITECTURE.md) for the project's MVVM boundaries and
composition.

## Pull requests

Pull requests must pass the required build check and resolve all review conversations before merging. Changes are merged into `main` with a merge commit.

By contributing, you agree that your contribution is licensed under the repository's MIT License.
