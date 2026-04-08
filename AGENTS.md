# AGENTS

## .NET Test Runs
- Never start more than one `dotnet test` process at a time for the same `.csproj`.
- For targeted .NET test runs, prefer `./scripts/run-dotnet-test-serial.ps1` so multiple `FullyQualifiedName` filters are merged into one invocation and the project lock prevents collisions.
- Invoke that script from the current PowerShell shell with `& "./scripts/run-dotnet-test-serial.ps1" ...`; never wrap it with `powershell.exe -File`, because that mode can leave leaked child processes and make Codex appear stuck.
- Prefer fixed preset scripts when they match the task:
  `./scripts/run-tests-services-regression.ps1`
  `./scripts/run-tests-phase42-regression.ps1`
  `./scripts/run-tests-plc-regression.ps1`
  `./scripts/run-tests-desktop-endpoints.ps1`
- If several test classes need validation in the same test project, pass them together via repeated `-FullyQualifiedName` values instead of spawning parallel test commands.
- After the same project has already built successfully in the current session, prefer `-NoBuild -NoRestore` on follow-up runs.

## Example
```powershell
./scripts/run-dotnet-test-serial.ps1 `
  -Project "Acme.Product/tests/Acme.Product.Tests/Acme.Product.Tests.csproj" `
  -FullyQualifiedName FlowExecutionServiceTests,ConnectionPoolManagerTests,InspectionRuntimeCoordinatorTests `
  -NoBuild `
  -NoRestore
```
