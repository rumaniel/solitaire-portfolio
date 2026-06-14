# Play Store Submission Checklist

Ordered step-by-step from zero to live. Check each box as you go.

## 1. Prerequisites

- [ ] Google Play Console account created ($25 one-time fee) — https://play.google.com/console
- [ ] Developer identity verified (ID + address)
- [ ] D-U-N-S number (if registering as an organization, not required for individuals)
- [ ] Privacy policy hosted at a public URL
  - Host: `https://studio-mangru.github.io/privacy` — **studio-wide, single URL** reused across every Studio Mangru app. This is the same URL bound in `AppConfig.asset → privacyPolicyUrl`.
  - Draft in `privacy-policy.md` covers the studio-wide framing + Solitaire Tower in the "Apps covered" section; add new apps to that section rather than creating new URLs.
  - Contact email already set to `mangru.studio@gmail.com`.
  - Only placeholder remaining: `[EFFECTIVE_DATE]` — fill with the actual launch date before publishing.
  - *(future migration)* Once the `mangru.dev` domain is wired, swap `AppConfig.asset → privacyPolicyUrl` and the Play Console URL field. The in-app button requires no rebuild if only the hosted page moves.

## 2. Unity project preparation

- [x] `ProjectSettings/ProjectSettings.asset` — `AndroidTargetSdkVersion` set to 35. Play Console enforces targetSdk ≥ 35 for new submissions and updates since the 2025 policy cycle (Aug 31 new apps, Nov 1 updates); the post-Unity 6.3 launch value of 34 caused the v16 AAB to be rejected. Unity 6.3.15f1 supports API 35 (Android 15) out of the box.
- [ ] `AndroidMinSdkVersion` 23 is fine (covers Android 6.0+, ~99% of devices)
- [ ] **Verify in-app privacy link opens the hosted URL** — the Settings panel's "Privacy Policy" button is already wired to `Application.OpenURL(AppConfig.PrivacyPolicyUrl)`. Before submission, confirm `AppConfig.asset → privacyPolicyUrl` matches the host you actually publish the policy at, and tap-test the button in a release build so Play reviewers find it.
- [ ] Bump `AndroidBundleVersionCode` to 1 (currently 1 ✓) and `bundleVersion` to `1.0.0` for first release (currently `0.0.1` — fine for testing, 1.0.0 is conventional for store)
- [ ] Confirm application identifier: `com.mangru.solitaire` ✓
- [ ] Verify keystore is set correctly (Player Settings → Publishing Settings → Keystore Manager)
- [ ] Strip unused assets: Build Settings → Player → Optimization → Managed Stripping Level = "Low" or "Medium"
- [ ] Enable IL2CPP + ARM64 (required for Play Store as of 2019)
  - Player Settings → Other Settings → Scripting Backend = IL2CPP
  - Target Architectures: ARMv7 + ARM64 checked

## 3. Build a signed release AAB

- [ ] File → Build Settings → Android → Build App Bundle (Google Play) ✓
- [ ] Build Type = Release, Development Build = unchecked
- [ ] Publishing Settings → Minify = Release (ProGuard / R8)
- [ ] Build → produces `solitaire.aab` (~40–80 MB expected)
- [ ] **Verify AAB with bundletool locally before upload:**
  ```bash
  bundletool build-apks --bundle=solitaire.aab --output=solitaire.apks --mode=universal
  ```
- [ ] Optionally side-load the resulting APK to your phone to smoke-test

## 4. Play Console — App setup

### Create app
- [ ] Play Console → All apps → Create app
- [ ] App name: **Solitaire Tower**
- [ ] Default language: **English (United States)** — Korean (ko-KR) translation is drafted in `listing-copy.md` and added as a translated listing in step 5
- [ ] App or game: **Game**
- [ ] Free or paid: **Free**
- [ ] Declarations: accept policies

### Dashboard → Set up your app (left sidebar)
- [ ] **App access** — declare any locked features (answer: all features available, no login wall)
- [ ] **Ads** — "No, my app does not contain ads"
- [ ] **Content rating** — fill questionnaire (expect Everyone / PEGI 3)
- [ ] **Target audience** — pick age range (13+ recommended)
- [ ] **News apps** — No
- [ ] **COVID-19 contact tracing** — No
- [ ] **Data safety** — fill the form to match `privacy-policy.md` §2. Declare the following data types:
  - **Personal info → User IDs**: Firebase Anonymous Auth UUID (for save sync); Firebase Installation ID (from Analytics)
  - **App activity**: page views and taps / in-app search history / other user-generated content — declare the Analytics `screen_view`, `session_start`, `user_engagement` events. Purpose: Analytics
  - **App info and performance**: crash logs, diagnostics, other app performance data (Crashlytics stack traces + Analytics device/OS/app-version metadata). Purpose: Analytics, App functionality
  - **Device or other IDs**: Android Advertising ID (via Firebase Analytics auto-collection where the OS exposes it). Purpose: Analytics
  - **Approximate location**: coarse geography derived from IP by Firebase Analytics. Purpose: Analytics
  - Data shared with third parties: None — Firebase is a processor under Google's DPA, not a "share" or sale
  - Security practices: data in transit encrypted (HTTPS); users can request deletion via `mangru.studio@gmail.com`
  - User data deletion: Yes — request path documented in privacy policy §8
- [ ] **Government apps** — No
- [ ] **Financial features** — None

## 5. Play Console — Store listing

### Main store listing (en-US default)
- [ ] App name: `Solitaire Tower` (from `listing-copy.md`)
- [ ] Short description: copy the primary from `listing-copy.md`
- [ ] Full description: copy the full draft from `listing-copy.md`
- [ ] App icon: upload `icon-512.png`
- [ ] Feature graphic: ⚠️ **refresh before upload** — current `feature-graphic.png` advertises Easthaven (not shipping in v1.0.0)
- [ ] Phone screenshots: upload `screenshots/01-lobby.png` → `08-codes.png` in order (1080×1920, en-US, 8 shots ready)
- [ ] 7" tablet screenshots: *(optional)* skip for launch
- [ ] 10" tablet screenshots: *(optional)* skip for launch
- [ ] TV banner: N/A
- [ ] Wear OS screenshots: N/A
- [ ] Promo video (YouTube URL): *(optional — record with Unity Recorder later and link)*

### Translated listing — Korean (ko-KR)
- [ ] Add language: Play Console → Store presence → Main store listing → Manage translations → Add Korean (ko-KR)
- [ ] App name: `솔리테어 타워` (from `listing-copy.md` Korean section)
- [ ] Short description: copy the primary Korean draft
- [ ] Full description: copy the full Korean draft
- [ ] Translated screenshots: *(optional — same image set works for both locales since the UI itself is localized at runtime)*

### Store settings
- [ ] App category: **Games → Card**
- [ ] Tags: `Solitaire`, `Klondike`, `Card`, `Casual`
- [ ] Contact details: email `mangru.studio@gmail.com`, website `https://www.mangru.dev`, phone (optional)
- [ ] External marketing: "No"

## 6. Play Console — Release

### Testing tracks (recommended before Production)
- [ ] **Internal testing** track first — upload the AAB, invite yourself via email, install from Play Store, verify it runs
- [ ] **Closed testing** next — invite 10–20 testers (friends, Reddit r/Unity3D, local game dev groups)
  - Required for production since Nov 2023: at least 12 closed testers for 14 consecutive days
- [ ] **Open testing** optional — public beta link

### Production release
- [ ] Release → Production → Create new release
- [ ] Upload signed AAB
- [ ] Release notes (en + ko): copy from `listing-copy.md` "Release notes" section
- [ ] Countries: start with Korea + English-speaking markets, expand after launch
- [ ] Submit for review
- [ ] First review typically 2–7 days

## 7. After first publish

- [ ] Monitor Android Vitals (crash rate, ANR rate) daily for first week
- [ ] Check Play Console → Quality → Ratings for user feedback
- [ ] Set up crash reporting — Firebase Crashlytics is already integrated ✓
- [ ] Add Korean translation (`ko-KR`) once English listing is stable
- [ ] Plan monthly content or patch cadence

---

## Common rejection reasons (pre-submit review)

- **Target SDK too low** → set to 34+ (step 2)
- **No privacy policy URL** → must be public and stable (step 1)
- **Data safety form doesn't match reality** → if Firebase collects UUID, declare it
- **Screenshots show unreleased features or dummy text** → our screenshots are real game state ✓
- **Feature graphic has fine-print / illegible text** → our current graphic is simple; readable at thumbnail size ✓
- **App name too similar to existing app** → "Solitaire Tower" search is clear as of this writing, but check before submitting

---

## Time estimate

| Stage | Time |
|-------|------|
| Unity build + signing | 30 min |
| Play Console account setup | 30 min (plus 1–2 day ID verification) |
| Listing completion (all forms) | 2–3 hours first time |
| Closed testing phase | 14 days (required for first publish) |
| Production review | 2–7 days |
| **Total from zero to live** | **~3 weeks** (most of which is Google's mandatory waiting) |
