# WOF conversation retrieval index

Conversation context is archived only when the task-history API proves the retrieved task has reached its end. A title or preview alone is not treated as a transcript.

## Fully retrieved

- `69b0c752-cd14-832b-8269-b90db1e31325` — **Wizards Only Fools Guide**
  - Kind: ChatGPT
  - Turns: 1
  - Retrieval page state: `hasMore=false`, `nextCursor=null`
  - Local archive: `Docs/Context/WIZARDS_ONLY_FOOLS_GUIDE.md`
  - Context value: early S&box-oriented staged-development roadmap; useful for product intent and the small-slice workflow, but not a canonical implementation source for the React or Unity code.

## Current active task

- `019fd7f8-ff8d-7020-ad85-4d67bf0b90f4` — **Assess WOF Unity Unreal migration**
  - Kind: Codex
  - Status at indexing: active
  - Not archived as a complete transcript because its active turn and future turns are not finished.

## Discovery limitation

The Codex app task-list API returned at most the 50 most recent task summaries and exposed no older-task cursor. Within that accessible window, the two tasks above were the only title/preview matches for `Wizards Only Fools` or `WOF`. This proves the guide transcript is complete; it does **not** prove that every older WOF conversation has been discovered. Do not describe the overall conversation archive as complete unless an older-history source becomes available and is exhausted.

