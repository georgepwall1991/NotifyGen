# AGENTS.md

## Cursor Cloud specific instructions

NotifyGen is a compile-time C# / .NET Roslyn source generator (a library/dev tool, not a
service). There are **no long-running services, servers, databases, ports, or `.env`
files**. "Running end to end" means building the solution, running the xUnit test suite,
and optionally running a sample app. Standard commands live in `README.md` and
`.github/workflows/ci.yml`; the notes below only cover non-obvious caveats.

### Toolchain
- The `.NET SDK` (8.0, 9.0, and 10.0) is installed under `~/.dotnet` and added to `PATH`
  via `~/.bashrc`. Interactive shells pick this up automatically. All three SDKs are
  required because the test/sample projects multi-target `net8.0;net9.0;net10.0`.
- There is no `global.json`, so the SDK version is not pinned.

### Build / test / run (from repo root `/workspace`)
- Build: `dotnet build --configuration Release` (builds all 6 projects; the WPF sample
  builds cross-platform via `EnableWindowsTargeting`).
- Test: `dotnet test` runs the whole matrix. To run a single framework use
  `dotnet test --framework net10.0` (also `net8.0` / `net9.0`). ~233 tests.
- Console sample (the quickest end-to-end demo of the generator):
  `dotnet run --project samples/NotifyGen.ConsoleSample -f net10.0`.

### Non-obvious caveats
- `dotnet run`/`dotnet test` with `--no-build` default to the **Debug** output. If you
  built with `--configuration Release`, you must pass `-c Release` alongside `--no-build`
  or it will fail with "No such file or directory".
- The **WPF sample** (`samples/NotifyGen.WpfSample`) compiles on Linux but only *launches*
  on Windows — do not try to run it here; building/testing it is fine.
- Because this is a source generator, generated code is produced at compile time. After
  changing generator logic, rebuild before re-running the samples/tests to pick up changes.
