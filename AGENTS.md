# Repository Guidance for Agents

## Communication

- Write all GitHub issue comments, pull request comments, pull request descriptions, commit messages, and other GitHub-facing text in English.
- Keep user-facing explanations in the language used by the user unless asked otherwise.

## Public and Protected API

- Preserve binary/source compatibility whenever possible. Do not remove or rename public members without an explicit request.
- When replacing an existing public member, keep the old member as an obsolete wrapper and delegate to the new implementation with the previous default behavior.
- Keep API wording consistent with existing naming. For example, prefer existing `Remove...` terminology over introducing `Delete...` for the same conceptual operation.
- Avoid unrelated API changes or side features while implementing a requested feature.

## XML Documentation

- Every new public API member must have XML documentation matching the quality of the surrounding API.
- Public enums and their values must be documented.
- Keep `<summary>` text short and focused, for example `Insert one or more columns.`.
- For new or substantially edited XML documentation, prefer Microsoft-style complete sentences in descriptive third-person form with a final period, for example `Inserts one or more columns.`.
- Treat broad cleanup of older XML documentation style as a separate follow-up task instead of mixing it into feature or targeted documentation commits.
- Put details such as zero-based indexing, insertion position, behavior contracts, and parameter semantics into `<param>`, `<returns>`, `<remarks>`, or `<exception>` elements as appropriate.
- Prefer simple grammar that is easy to understand for non-native English speakers.
- For overrides, always add an explicit `<inheritdoc/>` when the inherited documentation applies. Do not rely on implicit inherited documentation from an undocumented override.
- For overloads with mostly identical documentation, prefer `<inheritdoc/>` plus targeted `<param>`, `<returns>`, `<remarks>`, or `<exception>` overrides instead of copying large documentation blocks.
- Add `<remarks>` to inherited documentation only for specific behavior or limitations.

## Tests and Generated Files

- Add durable unit tests for new behavior.
- Test methods should preferably include short comments or XML summaries explaining why the test exists and what workbook behavior it verifies, but this is guidance and not a mandatory API documentation requirement.

## File Encoding and Line Endings

- Save text files as UTF-8 with BOM and CRLF line endings, matching `.editorconfig`.
- Keep `.gitattributes` line-ending rules intact and mark binary workbook/image/archive formats as binary.
- When normalizing encoding or line endings, keep that work in a separate mechanical commit whenever possible.
