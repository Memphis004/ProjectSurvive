# Gotcha — a test-assembly compile error makes a test run report the previous assembly's results

`run_tests` (the Editor pipeline used by `tools/test.ps1`) can answer with the
**previous** compiled assembly's payload while the new test file does not compile. The
result says `success: true` and a plausible pass count; the count is the only tell.

The game era saw `14 passed, success: true` while the new test had a `CS0117`. A
second, related trap: two runs in a row can return **identical per-test durations** —
the cached payload, not a new run.

**Always:**

- confirm the test you just added appears in the payload (by name, not by count);
- or confirm `compilationFailed` is false;
- treat identical per-test durations across two runs as "this is the old payload"
  until proven otherwise;
- keep the raw payload (`artifacts/dotnet/unity-<mode>-status.json`) so a count can
  be checked after the fact.

The harness polls for a run that either reported `running` first or differs from the
payload captured before submitting, but it cannot invent a test that never compiled.
