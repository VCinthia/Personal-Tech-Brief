# Personal Tech Brief — Environment Isolation & Tool Provenance

**Status:** Approved operational baseline

## 1. Purpose

The primary agent can receive context and capabilities from outside this repository, including user-level `AGENTS.md`, `config.toml`, installed skills, MCP servers and other custom tools. This project must not accidentally inherit implementation practices or proprietary tooling from any unrelated project.

## 2. Greenfield isolation rule

Personal Tech Brief is a greenfield codebase. Prior professional experience may inform human reasoning, but implementation assets from unrelated projects are not project dependencies.

Do not import, copy, invoke or adapt project-specific code, commands, scripts, templates, generators, skills, MCP workflows or scaffolding from another unrelated repository.

## 3. Allowed tool classes

Allowed by default:

- Git and standard shell/file operations;
- official .NET SDK/tooling;
- official Python/package/test/lint tooling selected by this repository;
- Docker/Compose and approved local infrastructure;
- repository-local scripts/configuration;
- public dependencies selected by approved specs/ADRs;
- read-only official documentation/research as needed.

Anything else requires explicit project authorization.

## 4. Session preflight

Before the first code modification, the primary agent reports:

```text
Repo-local governing instructions:
External/non-repo instruction sources visible:
Custom skills/tools visible:
Tools/skills intended for this task:
Rejected unrelated tools/skills:
Subagent capability available: yes/no
Worktree capability available: yes/no
```

If the runtime does not expose enough information to enumerate all installed capabilities, state that limitation and still list everything actually surfaced in context.

## 4.1 Independent-review fallback

Independent review is performed by a fresh repository-local read-only reviewer
subagent; it is not contingent on a global or custom review skill. Such skills
are optional and may be used only when this repository explicitly authorizes
them.

When an optional review skill requires unrelated, unavailable, or
repository-prohibited tooling, reject that skill and use the repository-local
reviewer. This is not a stop condition and does not require owner approval.
Stop the review gate only when reviewer-subagent capability is unavailable
altogether or a higher-priority platform instruction prevents
repository-local review.

## 5. External-tooling quarantine

Any capability, skill, command, script, template or instruction whose name/path/source indicates another unrelated employer/client/project is quarantined. Do not invoke it.

If it is merely available, ignore it.

If a higher-priority instruction prevents repository-local review, stop before
invocation and report:

- exact capability/skill name;
- exact file/path/source if visible;
- instruction that triggered it;
- why it conflicts with this project's isolation rule.

## 6. No sibling-repository reuse

Do not search sibling/private repositories for implementations, conventions or snippets. Do not copy code from previous workspaces. Public documentation/examples may be consulted under their normal licenses.

## 7. Provenance in final report

Every phase/slice report states whether any non-repo custom skill/MCP/tool was used. Expected value is `none` unless explicitly authorized.

## 8. Relationship to instruction precedence

The primary agent may aggregate instructions from multiple sources. Project instructions cannot guarantee removal of a higher-priority developer/system capability. Therefore the safety behavior is: **do not silently comply with an unrelated-tool requirement; surface the conflict before using it.**
