# `fluc` CLI output

FluNET has one command-line host for the current language: `fluc`.

## Verbosity

The default output is intentionally quiet: program output goes to stdout and
errors go to stderr. Add verbosity only when inspecting execution:

- `-v` prints run start/completion summaries.
- `-vv` adds stage progress.
- `-vvv` adds selected implementation metadata and timing details.

The flags can appear before or after the command. `-v`, `-vv`, and `-vvv` are
monotonic, so the most detailed flag wins.

## Color

`--color auto` is the default. It uses ANSI colors only when stderr is an
interactive terminal and honors `NO_COLOR`. `--color always` is useful for a
terminal wrapper; `--color never` is useful for logs and snapshot tests.

Green means successful progress, red means failure, yellow means handled
warning/failure, cyan means active progress, and gray is low-priority detail.
FluNET program output is never decorated by the renderer.
