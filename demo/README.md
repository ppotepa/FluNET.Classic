# FluNET.Classic demos

`showcase.flu` is the flagship program. It combines typed functions and records,
file and JSON pipelines, collection operations, conditions, bounded parallelism,
interpolation, and structured failure recovery in one deterministic local run.

The smaller scripts isolate the introductory examples:

- `hello.flu` — the smallest complete sentence.
- `pipeline.flu` — file input, typed pipeline continuation, and iteration.
- `records.flu` — an immutable typed record.

Run the showcase from the repository root:

```text
flu check .
flu format showcase.flu
flu plan .
flu run .
```

The optional file read near the end intentionally targets a missing file. It
demonstrates `ON FAILURE`; the program recovers and always executes `FINALLY`.
