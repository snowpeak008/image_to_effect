# Reading Validator and Build reports

Each report entry has `code`, `severity`, `path`, `message`, and sometimes `actualValue` and `allowedRange`. Correct only the reported issue and retain unrelated valid fields. Paths for stages and modules use IDs, not array indexes: `/stages/travel/modules/embers/parameters/rate`.

`error` blocks Build. `warning` and `info` do not, but a warning should be reviewed. Return the full corrected JSON object (or the corrected Patch operation array) to the same validation loop; do not reply with a prose description of a fix.

| Code family | Meaning and usual correction |
|---|---|
| `E100` | Unknown field. Remove it; v1 does not ignore invented fields. |
| `E101` / `E102` / `E103` / `E104` / `E105` | Missing field, wrong type, unsupported enum, malformed JSON, or non-finite number. Match the Schema exactly. `E103` includes the actual token and an explicit contract-ordered `allowedRange`; copy an allowed token exactly. |
| `E301`–`E316` | Recipe semantic issue: version, ID/uniqueness, duration, attachment, Catalog/template/kind/dimension, parameter declaration/type/range/finite value, or revision. Use the parameter table for facts. |
| `E400`–`E404` | Static budget error. Reduce the requested Catalog cost/duration or choose a legal structure; it is not a compiler crash. |
| `E500` / `E501` | A formal binding is unavailable or failed. Do not invent a binding or parameter; report the raw error to the maintainer. |
| `E700`–`E711` | Patch shape/path/target/revision/transaction error. Follow the Patch contract; never change `expectedRevision` speculatively. |

For a range error, use the inclusive `allowedRange` from the report. For example, an `E314` at `/stages/travel/modules/embers/parameters/rate` means change exactly that numeric parameter to a value in the live Manifest range. For `E311`, remove the named undeclared parameter rather than renaming it by guesswork. For `E308`, copy one listed `allowedRange` template ID **exactly** (including `PFT_2D_` prefix and case); never normalize, shorten, or infer an ID.

Patch failures may include `Failed operation index`. If it says `post-patch validation (unattributed)`, the final Recipe is invalid but no single operation can be safely blamed; inspect every operation and the full report. Build uses a full-rebuild fallback in v1 even when the impact report identifies only one changed module.
