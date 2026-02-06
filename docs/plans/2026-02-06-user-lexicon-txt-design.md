# User Lexicon TXT Design (Dual Track: Rime + Local)

Date: 2026-02-06  
Project: IME (Android .NET)

## Goals
- Provide a TXT import/export format for user lexicon.
- Import writes to both Rime user data and a local lexicon store.
- Export merges and de-duplicates across both stores.
- Use Settings page as the only entry point, with SAF file picker.

## TXT Format
- Encoding: UTF-8
- Ignore empty lines; lines starting with `#` are comments.
- Line format:
  - `pinyin<TAB>word1[:freq]|word2[:freq]|...`
- Examples:
  - `ni hao\t你好:120|你好呀|你好啊:30`

## Default Frequency Rules
- If a word has no frequency, assign defaults in order: 100, 90, 80... for that line.
- If a line has only one word and no frequency, default to 1.
- If a word appears multiple times, keep the highest frequency.

## Architecture
- `UserLexiconEntry`: model (pinyin, word, freq, source, updated_at).
- `UserLexiconTxt`: parse/serialize TXT.
- `IUserLexiconStore`: storage interface.
  - `LocalUserLexiconStore`: SQLite table `user_lexicon(pinyin, word, freq, updated_at)` with unique (pinyin, word).
  - `RimeUserLexiconStore`: write a Rime user lexicon file (e.g. `custom_phrase.txt`) in the user data dir.
- `UserLexiconService`: orchestrates import/export and merging.

## Data Flow
1) SAF open document -> read TXT stream.
2) `UserLexiconTxt.Parse` -> entries.
3) Merge and de-duplicate in memory (max freq wins).
4) Write to local store + Rime store.
5) Trigger Rime `Deploy()` and `SyncUserData()` if Rime write succeeded.

## Export Flow
- Read all entries from local store.
- Read Rime lexicon file (if available).
- Merge + de-duplicate, sort by pinyin then freq desc.
- SAF create document -> write TXT stream.

## Error Handling
- Format errors are non-blocking; record line number and skip that word.
- Storage errors are blocking for that store only; show partial success summary.
- Always restore UI state (buttons, progress) on failure.

## Testing Checklist
- Parsing: comments, empty lines, missing freq, multi-word line, invalid freq.
- Merge: same pinyin+word keeps higher freq.
- SAF: open/create works across Android 10+.
- Rime: imported words are effective after deploy/sync.
- Export: merged and sorted output is stable and deterministic.
