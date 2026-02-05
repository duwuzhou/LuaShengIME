# Candidate UI Design (Sogou-like)

Date: 2026-02-05
Project: IME (Android .NET)

## Goal
Build a Sogou-like candidate experience:
- Candidate bar: horizontal free-scroll list.
- Right-side “More” button opens a separate candidate panel.
- Preview row above the bar shows “current composition + primary candidate”.
- Candidate tap commits immediately.
- Candidate panel: bottom half-sheet, vertical list, infinite scroll.

## Summary of Decisions
- Candidate bar style: horizontal free-scroll (not page-snapped).
- Right button opens a bottom sheet candidate panel.
- Candidate bar page size: 6 per page (engine page size).
- Candidate panel: infinite scroll (no pagination UI).
- Preview row: show composition + primary candidate.
- Panel close: swipe down + close button.

## Architecture
### Components
- CandidateBar (UI): horizontal RecyclerView with “More” button on right.
- PreviewRow (UI): top row showing composition + primary candidate.
- CandidatePanel (UI): BottomSheetDialogFragment with vertical RecyclerView.
- CandidateController (logic): unified state and data flow for both bar + panel.

### State
CandidateController maintains:
- compositionText
- primaryCandidate
- pageSize (fixed at 6 for engine)
- pageNo / isLastPage
- candidateCache (list of candidates, cached by page)
- isLoading flag

### Data Flow
1) Key input -> IInputEngine.ProcessKey
2) On success, CandidateController fetches context/candidates
3) PreviewRow + CandidateBar update from controller state
4) CandidatePanel shares the same data source and cache

## Candidate Bar Behavior
- Free-scroll horizontal list.
- Lazy load next page when user scrolls near the end.
- Tap candidate: commit immediately, clear composition, close panel if open.
- Optional: long press shows annotation (pinyin/comment).

## Candidate Panel Behavior
- Bottom sheet half-screen.
- Vertical list, infinite scroll.
- Tapping a candidate commits and closes panel.
- Close via swipe down or close button.

## Engine Paging Strategy
Because the candidate bar is free-scroll, paging is handled internally:
- CandidateController requests next page when list end is near.
- If a candidate belongs to a non-current page, controller performs ChangePage to target page before SelectCandidateOnCurrentPage.
- Candidate panel can reuse cached pages to reduce paging cost.

## Error Handling
- If page change fails, keep current state and log error.
- If candidate select fails, show a light toast and keep candidates.

## Testing Checklist
1) Horizontal bar scroll loads next page near end.
2) Panel infinite scroll loads until last page.
3) Candidate click commits and clears composition.
4) Panel close preserves composition; commit clears both.
5) Backspace removes one pinyin char and updates candidates.
6) Simplified/Traditional settings do not break candidates.

## Open Questions (for later)
- Whether to show annotation by default or on long press.
- Whether preview row is always visible or hidden when empty.
- Whether to animate candidate bar updates for smoother UX.
