# Claude Code `--json-schema`: Rules, Limits & Gotchas

A practical reference for getting structured output out of `claude -p`. Verified
against **Claude Code 2.1.193** on native Windows / PowerShell, cross-checked
against Anthropic's structured-outputs platform docs. Items marked *(observed)*
were confirmed empirically in this version; items marked *(API)* come from the
underlying structured-outputs feature that the CLI wraps and inherits.

---

## TL;DR — the shape that works

```powershell
$PSNativeCommandArgumentPassing = 'Standard'        # PowerShell 7.2+; needed on Windows
$schema = Get-Content .\status.schema.json -Raw     # or a single-line inline string

claude -p "Do the task, then report status." `
  --output-format json `
  --json-schema $schema |
  ConvertFrom-Json | Select-Object -ExpandProperty structured_output
```

Three conditions must **all** hold, or you get plain text and no `structured_output`:

1. `--output-format json` is present (text mode never carries structured output).
2. The schema argument arrives as **valid inline JSON** (quoting is the #1 trap on Windows).
3. The **prompt asks for data that fits the schema**, and the schema uses only the **supported keyword subset**.

---

## 1. Command shape

```
claude -p "<prompt>" --output-format json --json-schema '<inline JSON Schema>'
```

- `--json-schema` requires `--output-format json`. With `text` (the default) there is no `structured_output` field at all.
- The schema value is **inline JSON only**. A **file path is NOT accepted** *(observed)* — passing `--json-schema "C:\path\schema.json"` makes Claude Code try to parse the path string as JSON and fail with `--json-schema is not valid JSON: ... Unexpected identifier "C"`. Read the file into a variable and pass its contents instead (see §3).

---

## 2. Where the output lands

The schema-conforming object appears in the top-level **`structured_output`** field — not in `result`.

- `structured_output` → the parsed object. **Use this.**
- `result` → a JSON *string* of the same data (handy for piping, but you'd have to re-parse).

```jsonc
{
  "type": "result",
  "subtype": "success",
  "stop_reason": "tool_use",          // success signal (see §10)
  "result": "{\"done\":true, ...}",   // JSON as a string
  "structured_output": {              // the object you want
    "done": true,
    "failed": false,
    "message": "...",
    "retry": { "afterSeconds": 0 }
  }
}
```

---

## 3. Passing the schema string (shell quoting)

The schema must reach Claude Code as one intact, valid-JSON argument. How you quote it depends on the shell. JSON is quote-heavy, which is exactly what breaks.

### Bash / macOS / Linux / WSL
Single quotes around the JSON work as written in the docs:
```bash
claude -p "..." --output-format json --json-schema '{"type":"object", ...}'
```

### Windows `cmd.exe`
`cmd.exe` does **not** treat single quotes as string delimiters. Wrap in double quotes and escape the inner double quotes:
```bat
claude -p "..." --output-format json --json-schema "{\"type\":\"object\", ...}"
```

### Windows PowerShell *(observed)*
Single-quoted inline JSON gets mangled at the `claude.cmd` shim boundary (cmd.exe re-parses the command line and eats the embedded `"`), so the schema silently fails to apply. Fix:

```powershell
$PSNativeCommandArgumentPassing = 'Standard'   # PS 7.2+ (default in 7.4+). Absent in Windows PowerShell 5.1.
$schema = '{"type":"object","additionalProperties":false,"required":["x"],"properties":{"x":{"type":"string"}}}'
claude -p "..." --output-format json --json-schema $schema
```

- Put this in `$PROFILE` or at the top of every script so you don't rely on a per-session toggle.
- Check `$PSVersionTable.PSVersion`. Under 5.1 the setting doesn't exist and the bug returns — run these calls under PowerShell 7.x.
- `(Get-Command claude).Source` ending in `.cmd` confirms the shim is the re-parsing layer.

### Maintainable: keep the schema in a file
Since a path isn't accepted, store the schema as a `.json` file and read it into a variable:
```powershell
$schema = Get-Content .\status.schema.json -Raw   # -Raw keeps it as one string; newlines are fine in JSON
claude -p "..." --output-format json --json-schema $schema
```

### Diagnostic: is the schema even arriving?
Pass deliberate garbage:
```
claude -p "say hi" --output-format json --json-schema 'THIS IS NOT JSON'
```
- **Errors** with `--json-schema is not valid JSON` → the argument is reaching Claude Code; any failure is now about prompt or schema content.
- **Succeeds** with a normal answer and no `structured_output` → the argument is being mangled/dropped (quoting problem).

---

## 4. What the prompt must do

`--json-schema` does **not** force arbitrary text into your shape. The model produces the structured object as a deliberate final step (you see `stop_reason: tool_use` and an extra turn when it works). If the prompt gives the model nothing that maps onto the schema, it answers in prose, ends with `stop_reason: end_turn` / `num_turns: 1`, and `structured_output` is absent.

- ❌ `"write a poem"` + a task-status schema → poem in `result`, no `structured_output`.
- ✅ `"write a poem, then report failed/done/a summary/retry seconds"` + the same schema → object emitted, `structured_output` populated.

Rule of thumb: the prompt should describe a deliverable whose answer **is** the schema.

---

## 5. Schema content rules

The schema is compiled into a grammar for constrained decoding, so only a **subset of JSON Schema** is usable.

### Supported
- Core: `type` (`object`, `array`, `string`, `integer`, `number`, `boolean`, `null`), `properties`, `required`, `items`.
- `enum`, `const`.
- `additionalProperties: false` — supported and **recommended**; the official SDKs add it to every object automatically.
- `description` — supported annotation; changing only descriptions does **not** invalidate the grammar cache.
- Nested objects/arrays.
- Union types via `anyOf` or type arrays (e.g. `["string","null"]`) — allowed but **expensive**; see limits.

### Not supported — strip these yourself *(API)*
Numeric / length constraints are **not enforceable** by the grammar:
- `minimum`, `maximum`
- `minLength`, `maxLength`
- (treat `minItems` / `maxItems` the same way)
- `format` is filtered to a supported subset; `pattern` (regex) support is limited — verify against the docs before relying on either.

The official **Python / TypeScript / Ruby / PHP SDKs** auto-remove these, fold them into the field `description`, and validate the response against your *original* schema. The raw `--json-schema` CLI flag is **not guaranteed to do this transformation** — so:

1. Remove unsupported constraints from the schema you pass.
2. Restate them in the `description` (e.g. *"Must be >= 0"*).
3. Re-validate the parsed `structured_output` in your own code.

### Annotations you can drop
`$schema` and `title` are harmless but unnecessary for the CLI; omit them to keep the schema lean.

### Worked example — the cleaned, working schema
```json
{
  "type": "object",
  "additionalProperties": false,
  "required": ["failed", "done", "message", "retry"],
  "properties": {
    "failed":  { "type": "boolean", "description": "Whether the task failed." },
    "done":    { "type": "boolean", "description": "Whether the task is complete." },
    "message": { "type": "string",  "description": "One-paragraph summary. Must be non-empty." },
    "retry": {
      "type": "object",
      "additionalProperties": false,
      "required": ["afterSeconds"],
      "properties": {
        "afterSeconds": { "type": "integer", "description": "Seconds to wait before retrying. >= 0; use 0 when no retry is needed." }
      }
    }
  }
}
```
Compared to a "textbook" schema, this drops `$schema`, `title`, `minLength: 1`, and `minimum: 0`, moving the last two into descriptions.

---

## 6. Complexity limits *(API)*

Larger schemas compile to larger grammars. Hard limits per request:

| Limit | Value | Notes |
|---|---|---|
| Strict tools per request | 20 | Only counts `strict: true` tools. |
| Optional parameters | 24 total | Any field **not** in `required`. Each one roughly doubles part of the grammar state space. |
| Union-type parameters | 16 total | `anyOf` or type arrays like `["string","null"]`. Exponential compilation cost — most expensive feature. |
| Compilation timeout | 180 s | Schemas that pass the explicit limits but still compile slowly fail here. |

Beyond these, an internal compiled-grammar-size cap can trigger a **400 "Schema is too complex for compilation"** even when every number above is satisfied.

**To reduce complexity:** make optional fields `required` where you can; flatten deep nesting; minimize union types; split very large schemas across multiple calls.

---

## 7. Output behavior & gotchas

- **Property ordering** *(API):* output orders **required properties first** (in schema order), then optional ones (in schema order) — not the literal order you wrote them. If exact order matters, mark everything `required`, or don't depend on order when parsing.
- **First-call latency / caching** *(API):* the grammar compiles on first use of a given schema, then is cached ~24h from last use. Changing the schema *structure* invalidates the cache; changing only `name`/`description` does not.
- **Token cost** *(API):* an extra system prompt describing the format is injected, so input tokens are slightly higher.
- **Refusals override the schema** *(API):* a safety refusal yields `stop_reason: "refusal"` (HTTP 200, still billed) and the output may not match the schema.
- **Truncation** *(API):* hitting the output cap gives `stop_reason: "max_tokens"` and possibly incomplete JSON — retry with a higher limit.
- **Incompatibilities** *(API):* not usable with Citations or message prefilling.
- **Scope** *(API):* the grammar constrains only Claude's final direct output, not tool calls, tool results, or thinking blocks.
- **Model support** *(API):* available on current models including Sonnet 4.6 (what the CLI defaults to here), Haiku 4.5, and the Opus 4.5–4.8 line.

---

## 8. Reading the result & exit codes

- Prefer `structured_output` (already an object) over re-parsing `result`.
- Success signals: `subtype: "success"`, `is_error: false`, `stop_reason: "tool_use"`, `num_turns >= 2`, and `structured_output` present.
- Schema-didn't-engage signal: `stop_reason: "end_turn"`, `num_turns: 1`, **no** `structured_output` → either the prompt didn't fit the schema (§4) or the argument didn't arrive (§3).
- **Exit codes:** `0` = success, non-zero = error. Branch on zero-vs-non-zero rather than hard-coding specific non-zero values; read the JSON for the precise reason.

---

## 9. Known-good PowerShell recipe (end to end)

```powershell
$PSNativeCommandArgumentPassing = 'Standard'

$schema = Get-Content .\status.schema.json -Raw

$json = & claude `
  --dangerously-skip-permissions `
  --output-format json `
  -p 'Run the build and tests, then report status.' `
  --json-schema $schema | ConvertFrom-Json

if ($json.is_error -or $json.stop_reason -ne 'tool_use' -or -not $json.structured_output) {
    throw "Structured output not produced. stop_reason=$($json.stop_reason)"
}

$status = $json.structured_output
# enforce the constraints the grammar can't:
if ([string]::IsNullOrWhiteSpace($status.message)) { throw "message was empty" }
if ($status.retry.afterSeconds -lt 0)              { throw "afterSeconds < 0" }

$status   # use it
```

---

## 10. Checklist

- [ ] `--output-format json` present.
- [ ] On Windows PowerShell: `$PSNativeCommandArgumentPassing = 'Standard'`, running under PS 7.x, schema passed via a variable (not a path).
- [ ] Schema passed as inline JSON, not a file path.
- [ ] No `minimum` / `maximum` / `minLength` / `maxLength` / unsupported `format`/`pattern` left in the schema — restated in `description` instead.
- [ ] `additionalProperties: false` on every object.
- [ ] Prompt describes a deliverable that *is* the schema.
- [ ] Within complexity limits (≤24 optional params, ≤16 union types, etc.).
- [ ] Reading from `structured_output`; checking `stop_reason: tool_use`; validating stripped constraints in your own code.
