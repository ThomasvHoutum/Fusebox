# Contributing to Fusebox

Thanks for taking the time to contribute.

## Before opening a pull request

1. Open an issue first for substantial changes so the approach can be discussed.
2. Keep each pull request focused on one change.
3. Build the solution in Release configuration:

   ```shell
   dotnet restore MCPLauncher.sln
   dotnet build MCPLauncher.sln --configuration Release --no-restore -p:TreatWarningsAsErrors=true
   ```

4. Add or update tests when the project area has automated coverage.
5. Update documentation when behavior or configuration changes.

## Pull requests

Pull requests must pass the required build check and resolve all review conversations before merging. Changes are squash-merged into `main`.

By contributing, you agree that your contribution is licensed under the repository's MIT License.
