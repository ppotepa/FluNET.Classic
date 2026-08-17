# Language

```text
GET TEXT FROM {file.txt} AS [lines]
GET TEXT FROM {a.txt} {b.txt} AS [allLines]
LOAD JSON FROM {config.json} AS [config]
SAVE [text] TO {output.txt}
TRANSFORM [text] USING BASE64 AS [encoded]
SAY "Hello [user.name]"

FILTER [users] WHERE Active IS true AND Age >= 18 AS [active]
IF [response.status] IS 200 THEN SAY "ok" ELSE SAY "failed"
FOR EACH [user] IN [users] THEN SAY "Processing [user.name]"
```

Qualifiers are first-class metadata. Verb overloads are selected by role shape, CLR type compatibility, cardinality, conversion cost and qualifier metadata. `params T[]` means a variadic sentence role; `T[]` without `params` is simply one collection-valued role.
