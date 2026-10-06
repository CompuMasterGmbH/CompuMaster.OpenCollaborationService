# Repository Guidance for Agents

## Communication

- Write all GitHub issue comments, pull request comments, pull request descriptions, commit messages, and other GitHub-facing text in English.
- Keep user-facing explanations in the language used by the user unless asked otherwise.

## DMS ecosystem and cross-repository changes

| Component | Repository | Responsibility |
|---|---|---|
| CompuMaster.Dms | [CompuMaster.Dms](https://github.com/CompuMasterGmbH/CompuMaster.Dms) | Provider-independent DMS workflows, provider adapters, and BrowserUI. |
| CompuMaster.Ocs | [CompuMaster.OpenCollaborationService](https://github.com/CompuMasterGmbH/CompuMaster.OpenCollaborationService) | ownCloud/Nextcloud OCS protocol, sharing, and account/group operations. |
| CompuMaster.Scopevisio.OpenApi | [CompuMaster.Scopevisio.OpenApi](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.OpenApi) | Scopevisio OpenScope REST API and authorization. |
| CompuMaster.Scopevisio.Teamwork | [CompuMaster.Scopevisio.Teamwork](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.Teamwork) | Teamwork integration connecting OpenScope authorization with CenterDevice clients. |
| CompuMaster.CenterDevice | [CompuMaster.CenterDevice.IO](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO) | CenterDevice REST and file-system SDK; the DMS package dependency is CompuMaster.CenterDevice.Rest. |

- Treat these libraries as one development ecosystem, but preserve their independent repositories, versioning, and standalone consumers. Inspect the affected dependency code and current issue/PR state when investigating DMS behavior; do not assume a fix belongs only in DMS.
- Put reusable protocol, authentication, transport, and SDK fixes in their owning library. Keep provider-neutral workflows, capability decisions exposed to consumers, and UI mapping in DMS. Record any temporary DMS workaround and the upstream issue required to remove it.
- Before changing an upstream library, create or reuse a focused issue in that repository with task/acceptance checkboxes. Link its implementing PR and the consuming DMS issue. In the DMS issue, add an explicit dependency link back to the upstream issue, with the affected package/API, integration order, exact commit or package version, and remaining verification.
- Preserve source/binary compatibility, existing synchronous APIs, and default behavior. Assess downstream effects on DmsUser ID/DisplayName/LoginName, resource ownership, sharing permissions/metadata, capabilities, and cancellation before changing underlying models or clients.
- Distinguish implementation, isolated tests, real-server tests, merge, package publication, and DMS consumption in issue checkboxes. An upstream PR or green old head does not establish current combined verification. Keep unresolved dependencies open and explain limitations.
- Coordinate immutable integration pins with other tasks before importing a moving feature branch. Never edit another task's checkout, discard its work, or silently upgrade dependencies. Verify the combined DMS build/tests with the exact intended dependency set.
- Use CompuMaster.Dms AGENTS.md as the baseline when introducing or updating agent guidance in these libraries. Adapt applicable API, XML documentation, issue tracking, test lifecycle, release, and cleanup rules; preserve repository-specific requirements. Do not copy DMS-only demo launch paths, issue numbers, package names, or workflow/lock names blindly.

## Public and Protected API

- Preserve binary/source compatibility whenever possible. Do not remove or rename public or protected members without an explicit request.
- When replacing an existing public member, keep the old member as an obsolete wrapper and delegate to the new implementation with the previous default behavior.
- Keep API wording consistent with existing naming. For example, prefer existing `Remove...` terminology over introducing `Delete...` for the same conceptual operation.
- Avoid unrelated API changes or side features while implementing a requested feature.

## XML Documentation

- Every new public API member and protected extension point must have XML documentation matching the quality of the surrounding API.
- Public enums and their values must be documented.
- Keep `<summary>` text short and focused, for example `Retrieves the resource metadata.`.
- For new or substantially edited XML documentation, prefer complete sentences in descriptive third-person form with a final period.
- Treat broad cleanup of older XML documentation style as a separate follow-up task instead of mixing it into feature or targeted documentation commits.
- Put details such as zero-based indexing, insertion position, behavior contracts, and parameter semantics into `<param>`, `<returns>`, `<remarks>`, or `<exception>` elements as appropriate.
- Prefer simple grammar that is easy to understand for non-native English speakers.
- For overrides, always add an explicit `<inheritdoc/>` when the inherited documentation applies. Do not rely on implicit inherited documentation from an undocumented override.
- For overloads with mostly identical documentation, prefer `<inheritdoc/>` plus targeted `<param>`, `<returns>`, `<remarks>`, or `<exception>` overrides instead of copying large documentation blocks.
- Add `<remarks>` to inherited documentation only for specific behavior or limitations.

## Tests and Generated Files

- Add durable unit tests for new behavior.
- Test methods should preferably explain the protocol, authorization, resource, or sharing behavior they verify; this is guidance, not a public API documentation requirement.

## Issue tracking

- Keep task and acceptance checkboxes current, and check an item only when its implementation and stated verification are complete.
- Before closing an issue, review its description, acceptance criteria, dependencies, and all checkboxes. Explain remaining or deferred work rather than marking it complete.
- Write GitHub-facing text in English. Keep changes focused on the owning issue.

## Test resource lifecycle and exclusive access

- Distinguish isolated tests from tests accessing real DMS servers. Add a failing regression before a fix where practical; report exactly which checks ran and what remains unverified. Builds or isolated tests do not prove server compatibility.
- Never print, commit, or copy credentials into issue/PR text. Do not assume locally available credentials authorize remote tests.
- Shared remote servers/accounts/test scopes require exclusive access across DMS, this repository, other ecosystem repositories, CI matrix jobs, scheduled workflows, and local sessions throughout setup, execution, and cleanup.
- Inspect the actual workflow lock/account mapping on the current branch and preserve serialization. GitHub Actions concurrency is repository-scoped: matching group names across repositories are NOT a cross-repository lock. A one-time check for running jobs is insufficient; require a coordinated exclusive window or a shared lock honored by every participant. Otherwise run isolated tests only.
- Before creating test resources, remove only verified test-owned leftovers and establish absence. Preserve configured permanent test roots and unrelated files, collections, accounts, and groups. Fail clearly if ownership, identity, or absence cannot be established.
- Track partial setup and always clean up in finally/teardown, removing links/children before parent resources as required by the backend. Make cleanup repeatable, verify removal, and preserve the original failure alongside cleanup failures.
- Select isolated tests explicitly for local-only changes. Do not run an entire remote suite casually, enable concurrent server mutations, or trigger remote workflows for documentation-only validation.

## Releases and branch cleanup

- Create releases only from the primary integration branch after the relevant PR is merged and required post-merge verification succeeds. Obtain explicit user approval immediately before release/publication; commit/push/merge approval alone is not release approval.
- Identify affected NuGet packages by their exact IDs, derive release notes from the full diff since the previous release, and prefix breaking entries with BREAKING CHANGE: in a dedicated section when appropriate.
- Do not claim an unpublished upstream commit is available to default package consumers. Coordinate publication and DMS dependency updates explicitly.
- Delete branches or worktrees only with authorization, after the tip is integrated and all required pipelines succeed. Retain them while checks are queued/running/failed/uncertain or unpublished work remains. Never switch, remove, or clean another active task's checkout.

## OCS-specific compatibility

- Keep ownCloud Classic, Nextcloud, and ownCloud Infinite Scale behavior distinct. Discover authenticated capabilities and server policies; product/version recognition alone does not establish supported operations.
- Preserve protocol IDs separately from display names and login names, source paths separately from recipient file_target values, permission-bit semantics, and original exceptions.
- Test parsing and capability differences in isolation. Remote user/group mutations require explicitly configured test recipients. Do not claim Infinite Scale or a server version is verified without actual test evidence.

## File Encoding and Line Endings

- Save text files as UTF-8 with BOM and CRLF line endings, matching `.editorconfig`.
- Keep `.gitattributes` rules intact and treat images, archives, and other binary assets as binary.
- When normalizing encoding or line endings, keep that work in a separate mechanical commit whenever possible.
