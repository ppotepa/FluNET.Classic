# Flu shell conformance

`0.3.0-alpha.1` establishes the native-process contract. It is the first step toward making Flu useful for the work normally done in `.sh` and `.ps1` files while keeping the canonical syntax as simple English sentences.

## Alpha.1 contract

| Capability | Status |
|---|---|
| Foreground native execution | Complete |
| Exact argument boundaries | Complete |
| Empty and Unicode arguments | Complete |
| PATH executable resolution | Complete |
| Explicit executable paths | Complete |
| Captured standard output | Complete |
| Captured standard error | Complete |
| Exit-code result | Complete |
| Working-directory request model | Complete |
| Environment override request model | Complete |
| Cancellation and process-tree termination | Complete |
| Process execution capability | Complete |
| Safe process planning | Complete |
| Verbose process events | Complete |
| Streaming byte pipelines | Planned for alpha.2 |
| Native stdin and redirection | Planned for alpha.2 |
| Background jobs | Planned for alpha.3 |
| Interactive shell session | Planned for alpha.3 |
| History, completion and prompt | Planned for alpha.4 |

## Canonical syntax

```flu
RUN {dotnet} WITH ARGUMENTS "--version" INTO [version].
RUN {git} WITH ARGUMENTS "status", "--short" INTO [status].
REQUIRE [status] SUCCEEDED.
```

The argument values are passed directly to the executable. Flu does not invoke an implicit `cmd`, PowerShell or Unix shell, and does not split or re-interpret argument strings after binding.

`ProcessResult` exposes `ProcessId`, `Executable`, `Arguments`, `ExitCode`, `Output`, `Error`, `StartedAt`, `Duration` and `Succeeded`.

## Safety contract

Native execution requires `process.execute`. `fluc plan` reports the requested and resolved executable, argument count, result type, capability and effective execution policy before running the process. Sensitive values are redacted in plans, traces, diagnostics and verbose output.

Process start failures, missing executables, cancellation and timeout are runtime failures. A process that starts and exits with a non-zero code still produces a `ProcessResult` with `Succeeded` set to `false`, allowing the program to inspect or explicitly require success.

## Conformance fixture

`tests/FluNET.Classic.ProcessFixture` is a platform-neutral executable used by tests for argument boundaries, stdout/stderr, exit codes, sleep/cancellation, environment and working-directory behavior. Tests must not depend on the quoting behavior of PowerShell, `cmd` or `/bin/sh`.
