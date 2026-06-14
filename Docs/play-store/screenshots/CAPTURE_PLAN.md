# Phone Screenshot Recapture Plan

The four legacy screenshots in this folder (`01-login.png` … `04-win.png`) were captured against the pre-W3 UI: 600×960, "60 FPS" debug overlay visible, plain Win modal without the cascade + confetti polish, lobby tiles still labeled "Coming Soon" for modes that contradict the listing copy. This document is the recipe for replacing them.

## Target

- **Count**: 8 phone screenshots (Play Store accepts 2–8; ship the maximum for shelf real estate)
- **Resolution**: 1080×1920 (9:16 portrait, the Play Store sweet spot)
- **Format**: PNG, no debug overlay, no FPS counter, no developer-only buttons
- **Naming**: `01-lobby.png` through `08-share.png` (sortable, descriptive)

## Pre-capture cleanup

- [x] **Disable FPS overlay** — confirmed off by user
- [ ] **Set Game View aspect to 1080×1920** — Game View → resolution dropdown → `+` → "Phone Portrait 1080×1920", Fixed Resolution
- [ ] **Confirm Login scene polish** — the legacy `01-login.png` exposed raw "User Id" / "Status" debug labels. If those are still visible at the current login flow, hide them or skip the login screenshot entirely (it's the least compelling of the eight; lobby + gameplay carry more weight).
- [ ] **Set locale to English for capture** — the Play Console default listing is en-US. Korean translated listing reuses the same images since the UI re-localizes at runtime; no separate ko capture needed unless the UI text on a screenshot must be Korean for the ko-KR shelf.
- [ ] **Use a deterministic deal code** for any in-game shots so the captured layout looks intentional. Pick a code where Klondike Draw 1 reaches a visually rich mid-game state.

## The 8-shot plan

Order matters — Play Store shows the first 2–3 shots above the fold on the listing.

| # | Filename | Scene / state | Setup notes | Why it's here |
|---|----------|---------------|-------------|---------------|
| 1 | `01-lobby.png` | Lobby with Klondike Draw 1 selected | Use a save state where Draw 1 has a "Continue" badge with a believable timer. Hide or remove the "Coming Soon" tiles before capture (or reorder so Draw 1/Draw 3 are the focus). | Establishes brand + shows the player is mid-progression already |
| 2 | `02-klondike-midgame.png` | Klondike Draw 1, mid-game, foundations partially built | Play 5–10 minutes from a known seed; aim for Hearts and Spades with A→4 stacked, tableau showing color alternation cleanly | Hero shot — proves the game looks good in motion |
| 3 | `03-drag.png` | Card or stack mid-drag, valid drop highlighted | Pause via OS or take screenshot mid-frame with `uloop screenshot` while a drag is active. The drop-target hint should be visible. | Communicates "this is a real touchscreen card game" |
| 4 | `04-hint-preview.png` | Hint ghost card flying from source to destination | Tap hint button, capture during the ghost preview animation (W3 polish) | Differentiator — most solitaire apps don't have animated hints |
| 5 | `05-win-cascade.png` | Win celebration, cards cascading down | Trigger a win, capture mid-cascade (`WinCascadeAnimator` is staggered ~50ms per card with a 1.2s flight; capture at ~0.5–1.0s into the sequence) | Showcases the W3 win polish |
| 6 | `06-win-panel.png` | Win panel with score / time / moves / share code | Capture after the cascade settles, with confetti still visible in background | Reinforces shareable deal codes (the unique selling point) |
| 7 | `07-stats.png` | Stats panel showing lifetime stats | Pre-populate with at least 5–10 wins so the numbers look real, not "1 game played" | Proof of depth — "I've been playing this a while" |
| 8 | `08-daily-or-share.png` | Daily Challenge entry point OR code-share toast | Daily Challenge tile if visually distinct in the lobby, or a "Code copied to clipboard" toast moment | Hooks the "every day a new deal" angle from the listing copy |

If any single shot is unobtainable, drop it and ship 7. Quality over count.

## Capture procedure

For each shot:

1. Set up the game state (deal code, mid-game position, animation moment).
2. With Game View at 1080×1920 Fixed Resolution, run:
   ```
   uloop screenshot
   ```
3. Screenshot lands in the project root or `Screenshots/` folder (uloop default). Move and rename to `docs/play-store/screenshots/0X-name.png`.
4. Verify in an image viewer: 1080×1920, no overlay artifacts, no debug text, no developer panels.

For the animated shots (`03-drag`, `04-hint-preview`, `05-win-cascade`):

- `uloop control-play-mode pause` mid-animation, then `uloop screenshot`. Resume + retry until the captured frame is the moment you want.
- For the cascade shot, the per-card stagger is short — likely 1–2 retries to get a frame with multiple cards in the air.

## Removing the legacy 4 screenshots

Once the 8 new shots are in place and confirmed, delete:

```
docs/play-store/screenshots/01-login.png
docs/play-store/screenshots/02-lobby.png
docs/play-store/screenshots/03-ingame.png
docs/play-store/screenshots/04-win.png
```

Update `README.md` and `checklist.md` to drop the ⚠️ warnings about screenshots being outdated.

## Quality checklist before submitting to Play Console

- [ ] All 8 are 1080×1920 (or at least 9:16 portrait, ≥720p)
- [ ] No "60 FPS" or other dev overlays visible
- [ ] No "User Id" / "Status" / debug labels exposed
- [ ] No NaughtyAttributes Inspector buttons accidentally captured
- [ ] First 3 shots are visually compelling at thumbnail size (Play Store renders them small in search results)
- [ ] Win cascade shot actually shows multiple ghost cards in the air, not a frozen pre-cascade state
- [ ] Stats panel shows non-trivial numbers (≥5 wins, real time totals)
- [ ] No personal info, real user IDs, or test build watermarks
