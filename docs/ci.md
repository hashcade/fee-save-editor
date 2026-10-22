# CI validation and releases

## Complete validation

`build.yml` runs two independent reusable workflows:

- `validate.yml` runs the complete business regression on Linux. The logic job
  checks release/CI guards, verifies that all 36 regression cases are assigned
  exactly once, runs pure data tests without starting Avalonia, and runs the full
  CLI suite. Seven GUI jobs run shell/layout, Main, Somniel, items, roster,
  Emblems and language cases in parallel. Cases report their individual timings.
- `platforms.yml` compiles on Windows x64, macOS ARM64, macOS x64 and Linux x64.
  A bounded platform smoke test renders all ten pages, switches English/Chinese
  at two sizes and verifies a lossless save copy. Each runner then publishes and
  tests its own self-contained package.

The default test-runner invocation still runs all cases. `--core-tests` does not
initialize the GUI. `--gui-suite <group>` runs only the named regression group;
`--suite-coverage` validates the partition without executing the cases.
`--platform-smoke` is not a replacement for complete validation.

## Package checks

ZIP checks retain executable permissions, macOS signatures/icons, bundle version,
runtime configuration and debug-file exclusions. The extracted CLI must report
the release version, start, inspect/copy a fixture, make an exactly reversible
resource edit, and insert Unicode amiibo variables with independently verified
bytes and checksums. The actual GUI process must remain running for three seconds
without an early exit, then the test terminates only that process. Linux uses
Xvfb for this native startup check. Headless page rendering is checked separately;
process liveness alone is not described as proof that a native window rendered.
The full CLI suite is not repeated against every ZIP.

## Reusing a successful build

Release packaging always compiles the tagged version and repeats all four
platform/package checks. Only complete business validation may be reused:

1. The release commit must have exactly one parent and differ from it only in
   `VERSION`. Any source, test, dependency or workflow change prevents reuse.
2. The parent must have a `push` run of this repository's `build.yml` on `main`
   with the exact parent SHA and the same head repository. PR/fork/manual runs
   are not accepted. Only the newest run/attempt is considered.
3. That run must finish with `success`. An in-progress run can be awaited for
   up to 20 minutes while release packaging proceeds independently. Missing,
   failed, cancelled, timed-out or unqueryable runs require complete validation.
4. Publication requires all four packages plus either a confirmed reused success
   or a newly successful complete validation workflow. Skipping validation for
   any other reason never permits publication.

`resolve_validation.py` only reads Git and GitHub state. It does not cancel runs,
rerun workflows, create releases or edit tags. Ordinary build concurrency also
does not cancel an already-running build. Updated workflows apply only to new
runs; existing releases keep the workflow definitions stored in their tags.

## Focused local checks

```sh
python scripts/test_ci.py
python scripts/test_release_script.py
dotnet build test/Gui.Smoke.csproj -c Release
dotnet test/bin/Release/net10.0/FeeEditor.Test.dll --suite-coverage
dotnet test/bin/Release/net10.0/FeeEditor.Test.dll --platform-smoke
```

Full regression stays in CI. A local package can be checked with
`python scripts/test_release_package.py <rid> --archive <zip>` without replacing
existing build artifacts. Timings must be measured from the new workflow; the
partition does not promise a fixed duration across hosted runner types.
