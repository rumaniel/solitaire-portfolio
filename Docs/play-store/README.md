# Google Play Store Submission Kit

Everything needed to submit **Solitaire Tower** (`com.mangru.solitaire`) to the Google Play Store.

```
Docs/play-store/
├── README.md                  ← this file (overview + requirements)
├── checklist.md               ← step-by-step submission checklist
├── listing-copy.md            ← store listing text en + ko (title, descriptions, keywords)
├── privacy-policy.md          ← studio-wide privacy policy draft
├── icon-512.png               ← 512×512 app icon (ready to upload)
├── feature-graphic.png        ← ⚠️ 1024×500 — needs refresh, still advertises Easthaven
└── screenshots/
    ├── CAPTURE_PLAN.md        ← recipe used to produce the shots below
    ├── 01-lobby.png           ← 1080×1920, en-US, Daily Challenge visible
    ├── 02-midgame.png         ← Klondike mid-game, foundations 3/4, move 83
    ├── 03-drag.png            ← card mid-drag (5♥ lifted)
    ├── 04-hint.png            ← hint highlight on source + target
    ├── 05-win-cascade.png     ← win cascade ghosts mid-flight + confetti
    ├── 06-win-panel.png       ← You Won panel with shareable deal code
    ├── 07-stats.png           ← lifetime stats panel (53 played / 26.4% win rate)
    ├── 08-codes.png           ← Pause panel exposing "Play with Code" share flow
    └── 09-fresh-deal.png      ← brand-new Klondike deal (move 0, all 7 columns dealt) — alternate
```

> **Play Store accepts up to 8 phone screenshots.** The 9 captures above are 1 over the cap; `09-fresh-deal.png` is staged as an alternate that can be swapped in for any of `01–08` (e.g. swap `02-midgame.png` if you'd rather lead with a clean opening layout). Pick the final 8 at upload time.

> **Status (post-W3 capture pass, 2026-05):** listing text and the 8 phone screenshots are submission-ready. `feature-graphic.png` is the only image asset still outdated — it advertises Easthaven, which doesn't ship in v1.0.0. Refresh tracked as Phase 3.

---

## Required Play Store assets (2025 spec)

### Graphics

| Asset | Spec | Status |
|-------|------|--------|
| **App icon** | 512×512 PNG, 32-bit with alpha, ≤1 MB | ✅ `icon-512.png` (305 KB) — current art, still good |
| **Feature graphic** | 1024×500 JPG or 24-bit PNG, **no alpha**, ≤15 MB | ⚠️ `feature-graphic.png` exists but advertises "Klondike \| Easthaven \| and more" — Easthaven not shipping in v1.0.0. **Refresh before submit.** |
| **Phone screenshots** | 2–8 images, JPG/PNG, 320–3840 px per side, max ratio 2:1, 16:9 or 9:16 preferred | ✅ 8 shots at 1080×1920 — see `screenshots/01-lobby.png` … `08-codes.png` (recipe in `screenshots/CAPTURE_PLAN.md`) |
| **Tablet 7" screenshots** | 1–8, 320–3840 px, max ratio 2:1 | ⚠️ not prepared (optional but boosts featuring eligibility) |
| **Tablet 10" screenshots** | 1–8, 1080–7680 px | ⚠️ not prepared (optional) |
| **Promo video** | YouTube URL, 16:9 landscape, loopable | ⚠️ not prepared — see `Docs/media-capture-guide.md` for Unity Recorder workflow |

### Listing text (see `listing-copy.md` for drafts)

| Field | Max length | en-US | ko-KR |
|-------|------------|-------|-------|
| **App name (title)** | 30 chars | ✅ drafted | ✅ drafted |
| **Short description** | 80 chars | ✅ drafted | ✅ drafted |
| **Full description** | 4000 chars | ✅ drafted (~1500 chars) | ✅ drafted |
| **Release notes (v1.0.0)** | 500 chars | ✅ drafted | ✅ drafted |

### Binary

| Item | Status |
|------|--------|
| **Signed AAB** (Android App Bundle) | ⚠️ user must build from Unity (Build Settings → Android → Build App Bundle) |
| **Target API level** | ⚠️ **set `AndroidTargetSdkVersion` in ProjectSettings.asset** — currently `0` (auto). Play Console requires API 34 (Android 14) or higher for new apps as of Aug 2024. Unity 2022.3 should default to 34 but verify. |
| **Keystore (release signing)** | ✅ `mangru.keystore` exists at project root (not in repo) |

### Play Console form items (user must complete in browser)

| Item | Notes |
|------|-------|
| Developer account | $25 one-time fee; Google Play Console |
| Privacy policy URL | **Required**. Host: `https://studio-mangru.github.io/privacy` (studio-wide, single URL reused across all Studio Mangru apps; matches `AppConfig.asset → privacyPolicyUrl`). Draft ready at `privacy-policy.md`; only `[EFFECTIVE_DATE]` left to fill |
| Content rating | Questionnaire in Play Console → IARC rating (likely Everyone / PEGI 3 for solitaire) |
| Target audience | Play Console → ages. Choose age range |
| Data safety form | Declare Firebase Auth UUID, Crashlytics diagnostics, Firebase Analytics (events + device/OS metadata + Android Advertising ID + coarse IP-derived location). Detailed field mapping is in `checklist.md` §4. |
| Ads declaration | No ads in current build |
| In-app purchases | None |
| Pricing & distribution | Free, select countries |
| App category | Games → Card |

---

## What's ready, what's your job

### Ready to upload as-is
- `icon-512.png`
- All 8 phone screenshots in `screenshots/` (1080×1920, en-US)
- All listing text in `listing-copy.md` — both English and Korean drafts

### Needs refresh before submit
- `feature-graphic.png` — current asset advertises Easthaven; v1.0.0 ships Klondike-only

### You still need to
1. **Refresh feature graphic** (1024×500, no alpha) — drop the false "Easthaven" callout, focus on Klondike + Daily Challenge
2. **Build a signed release AAB** in Unity (see `checklist.md` §3)
3. **Host the privacy policy** at the URL configured in `AppConfig.asset → privacyPolicyUrl` (currently `https://studio-mangru.github.io/privacy`), or change that field to match whichever host you actually publish at. The in-app Settings → Privacy Policy button is already wired to open this URL.
4. **Complete the Play Console questionnaires** (content rating, data safety, ads, target audience)
5. *(optional but recommended)* Record `hero.gif` / promo video with Unity Recorder — see `Docs/media-capture-guide.md`
6. *(optional)* Capture tablet screenshots in Unity Game View at 1024×600 and 1280×800

See `checklist.md` for the ordered step-by-step.
