# Privacy Policy — What's Needed and What to Publish

This document answers three questions:

1. **What does Google Play require?**
2. **What does Solitaire Tower actually collect?** (code audit, not marketing copy)
3. **What do I publish, and where?** (ready-to-host template + hosting recipe)

> **Scope decision:** Studio Mangru publishes **one studio-wide Privacy Policy** at a single URL and reuses it across every Play Store listing. New apps are added to the "Apps covered" section of the same document instead of getting their own URL. This matches the industry-standard pattern used by Supercell, Cysharp, HoYoverse, and other multi-app studios.

> **Submission readiness:** The in-app privacy entry point is already wired — `Assets/Prefabs/UI/SettingPanel.prefab` has a "Privacy Policy" button bound to `SettingPanelView.privacyButton`, which calls `Application.OpenURL(AppConfig.PrivacyPolicyUrl)`. The URL is configured in `Assets/ScriptableObjects/App/AppConfig.asset` (currently `https://studio-mangru.github.io/privacy`). Remaining pre-submission work:
>
> 1. **Publish the policy at `https://studio-mangru.github.io/privacy`** (GitHub Pages under the `studio-mangru` account — see "Hosting recipe" below). The page must return 200 from a fresh browser before the AAB is uploaded. The same URL is already bound in `AppConfig.asset`, so no code/prefab change is needed.
> 2. **Tap-test the button in a release build** to confirm the OS browser opens the hosted URL.
>
> **Known limitation (intentional, not a blocker):** No in-app Crashlytics opt-out UI. The policy below reflects this honestly — to stop future crash uploads, a user must uninstall the app. Track this as a future enhancement; do not list it as a user right we can satisfy in-product.

---

## 1. What Google Play requires

Per [Google Play Developer Program Policies](https://support.google.com/googleplay/android-developer/answer/10144311), every app that collects **personal or sensitive user data** (or has SDKs that do) must:

- Link to a **privacy policy on a public URL** from both the Play Console listing AND from within the app itself
- The policy must cover:
  - Developer identity + contact
  - Types of personal and sensitive data collected
  - How the data is used
  - Third parties it's shared with
  - Security practices
  - Data retention and deletion policy

"Personal or sensitive data" is interpreted broadly. Even an anonymous UUID + crash reports (our case) count. **You need a policy.**

The policy URL can be **shared across multiple apps** as long as it accurately describes each one. Per-app URLs are not required.

Additional jurisdiction-specific requirements if distributing worldwide:

| Region | Framework | Key extra |
|--------|-----------|-----------|
| EU | **GDPR** | Legal basis for processing, user rights (access/rectify/erase/portability), DPA complaint path |
| California | **CCPA / CPRA** | Right to know, right to delete, right to opt out of "sale" (we don't sell data) |
| Korea | **개인정보보호법** (PIPA) | 수집·목적·항목·보유기간, 제3자 제공, 책임자 지정 |
| Global minors | **COPPA / GDPR-K** | Only if targeting under-13s. Set target audience to 13+ to avoid this scope |

---

## 2. What Solitaire Tower actually collects — verified by code audit

### Data transmitted off-device

| Source | What | Purpose | Retention |
|--------|------|---------|-----------|
| `FirebaseAuthGateway.GetUuid()` | Anonymous Firebase UUID (random string, no personal info) | Cross-device save sync (when enabled) | Lives in Firebase project until user requests deletion |
| Firebase Crashlytics (consent-gated; off by default) | Stack trace, device model, OS version, app version, install ID (separate from auth UUID) | Crash diagnostics to fix bugs | 90 days per Firebase default |
| Firebase Analytics (consent-gated; off by default) | Automatic events (`first_open`, `session_start`, `screen_view`, `user_engagement`, `app_update`), device model, OS version, language, approximate geography derived from IP, Firebase Installation ID | Aggregate usage analytics — understand retention, session length, and feature adoption to prioritize development | Up to 14 months for user-level data (Firebase default, configurable in console); aggregate reports retained longer |

The Android Advertising ID is explicitly **not** collected: the `com.google.android.gms.permission.AD_ID` permission is stripped from the merged manifest, and `google_analytics_adid_collection_enabled` is set to `false`. Both Analytics and Crashlytics auto-collection are disabled at SDK boot via Android manifest meta-data and only enabled at runtime after the user accepts the consent dialog on first launch (`Service.ConsentService.IConsentService.MarkAccepted()`).

### Data stored only on device (never transmitted)

- `LocalAuthGateway` — UUID cached in `PlayerPrefs` (fallback when Firebase unavailable)
- `LocalGameSnapshotRepository` — current-game snapshots in `Application.persistentDataPath`
- `LocalStatsRepository` — lifetime stats (wins, streaks, best scores) in `Application.persistentDataPath`

### What's NOT collected (important to state)

- No email, name, phone number, or address
- No precise location (GPS / fine location permissions are not requested)
- No contacts, calendar, photos, microphone, camera
- No third-party analytics beyond Firebase Analytics (no GameAnalytics, AppsFlyer, Adjust, Amplitude)
- No in-app purchases, no ads
- No social logins (Google Play Games Services NOT integrated)

**SDK inventory** (verified in `Packages/manifest.json` and `Assets/Firebase/`):
- Firebase App (dependency only)
- Firebase Auth (anonymous sign-in)
- Firebase Crashlytics (crash reports)
- Firebase Analytics (consent-gated; collects standard GA4 events + device/OS metadata; Android Advertising ID is not collected)
- No other data-collecting SDKs

---

## 3. What to publish

### Primary: Studio Mangru Privacy Policy (single URL, all apps)

Publish once at **`https://studio-mangru.github.io/privacy`**, reuse in every Play Console listing. Solitaire Tower is the first entry in the **Apps covered** section. When you ship a second game, add it to that section — no new URL, no new page. Migrate later to an owned domain (e.g. `www.mangru.dev/privacy`) without an app rebuild — only `AppConfig.asset → privacyPolicyUrl` and the Play Console URL field change.

### Secondary: Korean version

Co-host on the same page (after the English version, separated by `---`) so Korean users see it without a separate URL. This keeps you aligned with `개인정보보호법` preference for native-language notice.

### Where to host (ranked by fit)

| Option | Effort | Pros | Cons |
|--------|-------:|------|------|
| **GitHub Pages on `studio-mangru.github.io/privacy`** ⭐ chosen | 10 min | No DNS setup; version-controlled; matches existing `AppConfig.asset` value | URL tied to GitHub account name (mitigate by migrating to owned domain later) |
| `mangru.dev` at `/privacy` (future owned domain) | 15 min | Branded URL; survives GitHub account churn | Requires DNS pointing + migration swap later |
| Notion / Google Sites public page | 5 min | Fastest | Unstable URLs, weaker branding |
| `iubenda` auto-generated policy | 0 min | Lawyer-reviewed templates | $36/year to remove attribution |

---

## Privacy Policy — English (ready to publish)

Copy everything below the `---` to a new file, replace `[EFFECTIVE_DATE]` with the actual launch date, and publish. All other fields (email, developer, contact, homepage) are already set to the final values.

---

### Studio Mangru Privacy Policy

**Effective date:** [EFFECTIVE_DATE]  
**Controller:** **Studio Mangru** ([Homepage](https://www.mangru.dev))  
**Contact:** mangru.studio@gmail.com

This Privacy Policy explains what information the apps published by Studio Mangru (collectively, "our Apps") collect, how it is used, and your choices. It applies to every app listed in section 1.

#### 1. Apps covered

This policy covers:

- **Solitaire Tower** — Android (`com.mangru.solitaire`)

New apps will be listed here when published. Any app not listed above is not covered.

All current apps share the same SDK stack and data practices described below. If a future app requires additional data (for example, ads or in-app purchases), we will publish an update to this policy before the new app's release.

#### 2. What we collect

**Stored only on your device (never transmitted):**
- Game progress for the current session (cards, moves, undo history)
- Lifetime statistics (games played, wins, streaks, best scores)
- A local identifier used to associate saves with this installation

**Transmitted to Firebase (a data processor operated by Google LLC):**
- An **anonymous user identifier** generated by Firebase Authentication. This identifier is not linked to your name, email, Google account, or any other real-world identity.
- **Crash diagnostics** via Firebase Crashlytics: stack traces, device model, operating system version, app version, and a Crashlytics install identifier (separate from the auth identifier). Collection is **disabled by default** at SDK boot and only enabled after you accept the consent dialog on first launch; once enabled, reports are sent automatically when the app crashes or on next launch after a crash.
- **Aggregate usage data** via Firebase Analytics (Google Analytics for Firebase / GA4). Collection is **disabled by default** at SDK boot and only enabled after you accept the consent dialog. Once enabled, the SDK collects: standard events (`first_open`, `session_start`, `screen_view`, `user_engagement`, `app_update`, `app_remove`), device model, OS version, app version, language, approximate geography derived from IP address, and a Firebase Installation ID. **The Android Advertising ID is not collected** — the app strips the `AD_ID` permission from the merged manifest and disables `google_analytics_adid_collection_enabled`. Used only in aggregate to understand overall usage patterns; we do not tie analytics events to your name, email, or other real-world identity.

#### 3. What we do NOT collect

We do not collect your name, email address, phone number, postal address, precise location (GPS), contacts, calendar, photos, microphone or camera input. We do not serve ads. We do not sell personal data to advertisers, brokers, or analytics providers. We do not use third-party behavioral-analytics SDKs beyond Firebase Analytics (see section 2). There are no in-app purchases in any app covered by this policy.

#### 4. Why we collect it

- **Anonymous identifier**: so your saves can be restored after reinstall or device migration.
- **Crash diagnostics**: so we can find and fix bugs that cause crashes.
- **Aggregate analytics**: so we can see (at a population level) what game modes are played, how long sessions run, and whether updates improve retention — this guides what we build next. We do not review individual user journeys.

#### 5. Legal basis (EU / GDPR users)

Processing relies on our **legitimate interest** in providing core app functionality (save sync), maintaining quality (crash fixes), and understanding aggregate usage (analytics). You can opt out of analytics and crash reporting as described in section 8.

#### 6. Third parties

We use the following service as a **data processor** (they process data on our behalf under a contract; we do not sell data to them):

- **Google LLC / Firebase** — Authentication, Crashlytics, Analytics. See [Firebase Privacy and Security](https://firebase.google.com/support/privacy) and [Google Analytics for Firebase privacy](https://firebase.google.com/support/privacy/manage-iads-data).

We do not share your data with advertisers, brokers, or anyone else.

#### 7. Retention

- Anonymous identifier and associated save data: retained in Firebase until you request deletion (section 8).
- Crash reports: retained by Firebase for 90 days by default.
- Analytics data: user-level data retained up to 14 months (Firebase default); aggregate reports retained longer and cannot be tied back to individuals.
- Local data on your device: removed when you uninstall the App.

#### 8. Your rights

You may contact **mangru.studio@gmail.com** at any time to exercise:

- **Access** — a copy of any data linked to your anonymous identifier.
- **Deletion** — we will delete your Firebase record and any linked saves.
- **Delete crash reports** — Firebase Crashlytics stores each install under a separate identifier (the "Crashlytics install ID") that is generated on your device and is not shown anywhere in the app. If you want your crash records removed, contact us with your approximate install date, device model, OS version, and app version; we will locate and delete any matching reports from our Firebase project on a best-effort basis.
- **Stop future crash reports** — declining the consent dialog on first launch prevents Crashlytics from ever collecting. Once accepted, the only available way to stop future uploads from your device is to uninstall the app (an in-app toggle is planned).
- **Limit analytics tracking** — declining the consent dialog on first launch prevents Firebase Analytics from ever collecting. The app does not collect the Android Advertising ID at all, so the OS-level "Delete advertising ID" / "Opt out of Ads Personalization" settings are not relevant to this app's analytics. An in-app analytics toggle is planned.
- **Complaint** — EU users may lodge a complaint with their local Data Protection Authority.

To help us find your record, you can copy your anonymous identifier from the in-app Settings screen (the "User ID" field with the Copy button), or mention the app name and approximate install date.

#### 9. Children

Our apps are rated for general audiences but are not directed to children under 13. We do not knowingly collect data from children under 13. If you believe we have, contact mangru.studio@gmail.com and we will delete it.

#### 10. Security

Data in transit is encrypted using HTTPS. Firebase services run on Google infrastructure; see Google's security documentation. Because we collect only an anonymous identifier and automatic crash diagnostics, the personal impact of any compromise is minimal.

#### 11. Changes to this policy

We may update this policy to reflect changes in our apps, added apps, or applicable law. Material changes will be announced in the relevant app's release notes, and the **Effective date** above will be updated.

#### 12. Contact

Questions, requests, or complaints: **mangru.studio@gmail.com**  
Homepage: https://www.mangru.dev

---

## Privacy Policy — 한국어 (참고 번역 — 배포 전 법률 검토 권장)

아래는 위 영문 정책의 한국어 대역입니다. 한국 배포 시 개인정보보호법(개인정보 보호법) 준수 조항을 간단히 포함했습니다.

### Studio Mangru 개인정보 처리방침

**시행일:** [EFFECTIVE_DATE]  
**개인정보처리자:** **Studio Mangru** ([Homepage](https://www.mangru.dev))  
**문의:** mangru.studio@gmail.com

본 방침은 Studio Mangru 가 배포하는 앱(통칭 "본 앱들")에 적용되며, 1항에 나열된 모든 앱을 대상으로 합니다.

#### 1. 적용 대상 앱

- **솔리테어 타워** — Android (`com.mangru.solitaire`)

신규 앱 출시 시 이 목록에 추가됩니다. 목록에 없는 앱은 본 방침 적용 대상이 아닙니다.

현재 본 앱들은 동일한 SDK 및 데이터 처리 방식을 공유합니다. 향후 앱이 추가 데이터(예: 광고, 인앱 결제)를 수집하게 될 경우, 해당 앱 출시 **전에** 본 방침을 갱신합니다.

#### 2. 수집하는 정보

**기기에만 저장 (외부 전송 없음):**
- 현재 게임 진행 상태, 이동/Undo 이력
- 누적 통계 (승수, 연승, 최고 점수 등)
- 설치별 로컬 식별자

**Firebase(Google LLC 제공, 수탁자)로 전송:**
- **익명 사용자 식별자**: Firebase Authentication 이 생성하는 무작위 문자열. 이름, 이메일, 구글 계정 등 어떤 개인 정보와도 연결되지 않습니다.
- **크래시 진단 정보**: Firebase Crashlytics — 스택 트레이스, 기기 모델, OS 버전, 앱 버전, Crashlytics 고유 설치 ID. SDK 부팅 시 **기본 비활성** 상태이며, 첫 실행 시 동의 다이얼로그를 수락한 이후에만 활성화되어 크래시 발생 시 자동 업로드합니다.
- **집계 이용 통계**: Firebase Analytics(Google Analytics for Firebase / GA4). SDK 부팅 시 **기본 비활성** 상태이며, 첫 실행 시 동의 다이얼로그를 수락한 이후에만 활성화됩니다. 활성화 후 수집: 표준 이벤트(`first_open`, `session_start`, `screen_view`, `user_engagement`, `app_update`, `app_remove`), 기기 모델, OS 버전, 앱 버전, 언어, IP 기반 대략적 지역, Firebase Installation ID. **안드로이드 광고 ID 는 수집하지 않습니다** — 병합된 manifest 에서 `AD_ID` 권한을 제거하고 `google_analytics_adid_collection_enabled` 를 비활성화 처리했습니다. 개별 이용자를 추적하지 않고 전체 이용 경향 파악에만 사용합니다.

#### 3. 수집하지 않는 정보

이름, 이메일, 전화번호, 주소, 정확한 위치(GPS), 연락처, 사진, 마이크, 카메라, 인앱 결제는 수집하지 않습니다. 광고를 표시하지 않으며, 광고주·데이터 브로커에게 데이터를 판매하지 않습니다. Firebase Analytics 외의 제3자 행동 분석 SDK(GameAnalytics, AppsFlyer, Adjust, Amplitude 등)는 사용하지 않습니다.

#### 4. 수집 목적

- 익명 식별자: 앱 재설치·기기 변경 시 세이브 복구
- 크래시 진단: 버그 수정·안정성 개선
- 집계 분석: 게임 모드 선호·세션 길이·업데이트 효과를 집계 수준에서 파악해 개발 우선순위 결정. 개별 이용자 행동은 추적하지 않습니다.

#### 5. 법적 근거 (EU / GDPR)

개인정보 처리는 서비스 핵심 기능 제공(세이브 동기화), 품질 유지(크래시 수정), 그리고 집계 수준의 이용 경향 파악(분석)이라는 **정당한 이익**에 근거합니다. 분석 및 크래시 수집 관련 이용자 권리는 §8 이용자 권리를 참고하세요.

#### 6. 제3자 제공·위탁

데이터 처리 업체(수탁자): **Google LLC / Firebase** (Authentication, Crashlytics, Analytics). Firebase 개인정보 정책: https://firebase.google.com/support/privacy

그 외 제3자에게 제공하거나 판매하지 않습니다.

#### 7. 보유 기간

- 익명 식별자·연관 세이브 데이터: 이용자 요청 시까지 Firebase 에 보관
- 크래시 리포트: Firebase 기본 설정(90일)
- 분석 데이터: 이용자 단위 데이터는 최대 14개월(Firebase 기본값), 집계 리포트는 그 이후에도 보관되나 개인 식별 불가능
- 기기 내 데이터: 앱 삭제 시 제거

#### 8. 이용자 권리

**mangru.studio@gmail.com** 으로 요청 시 처리합니다:
- **열람**: 익명 식별자와 연결된 데이터 제공
- **삭제**: Firebase 인증 레코드 및 연관 세이브 삭제
- **크래시 리포트 삭제**: Firebase Crashlytics는 인증 UUID와 별개의 install 식별자를 사용하며, 이 식별자는 앱에 노출되지 않습니다. 크래시 리포트 삭제를 원하시면 대략적인 설치일, 기기 모델, OS 버전, 앱 버전을 알려주시면 최선을 다해 해당 리포트를 찾아 삭제합니다.
- **향후 크래시 수집 중단**: 첫 실행 시 동의 다이얼로그를 거부하면 Crashlytics 수집이 시작되지 않습니다. 동의 후에는 앱 제거가 유일한 중단 수단입니다(인앱 토글 추가 예정).
- **분석 추적 제한**: 첫 실행 시 동의 다이얼로그를 거부하면 Firebase Analytics 수집이 시작되지 않습니다. 본 앱은 안드로이드 광고 ID 를 수집하지 않으므로 OS 의 "광고 ID 삭제"·"맞춤 광고 선택 해제" 설정은 이 앱의 분석과 무관합니다. 인앱 분석 토글은 추가 예정입니다.
- **신고**: 개인정보 보호에 관한 신고는 개인정보보호위원회(국번없이 182)

레코드 식별을 위해, 인앱 설정 화면의 "User ID" 항목(복사 버튼 포함)에서 익명 식별자를 확인해 함께 전달해주시면 처리가 빨라집니다. 혹은 앱 이름과 대략적인 설치일을 알려주셔도 됩니다.

#### 9. 아동

13세 미만 대상 앱이 아니며, 13세 미만의 데이터를 의도적으로 수집하지 않습니다.

#### 10. 안전 조치

전송 구간 HTTPS 암호화. Firebase 서비스는 Google 인프라에서 운영되며 Google 의 보안 기준을 따릅니다.

#### 11. 변경 고지

본 방침은 앱 또는 관련 법 개정에 따라 갱신될 수 있으며, 중요 변경 사항은 관련 앱의 릴리스 노트와 **시행일** 수정으로 안내합니다.

#### 12. 개인정보 보호 책임자 및 문의

책임자: Studio Mangru / 문의: mangru.studio@gmail.com  
홈페이지: https://www.mangru.dev

---

## Hosting recipe — `studio-mangru.github.io/privacy`

GitHub Pages under the `studio-mangru` user account hosts the policy. The user-site repo pattern means a single site covers the whole studio; `/privacy/index.md` is one page inside it.

```bash
# Create (or pull) the user-site repo
git clone https://github.com/studio-mangru/studio-mangru.github.io.git
cd studio-mangru.github.io
mkdir -p privacy

# Copy the English + Korean draft from privacy-policy.md into privacy/index.md,
# replace [EFFECTIVE_DATE] with the launch date, then:
git add privacy/
git commit -m "Studio Mangru privacy policy"
git push
# Live at https://studio-mangru.github.io/privacy/ within ~1 minute
```

### Verify before submission

1. Open `https://studio-mangru.github.io/privacy` in a fresh/private browser window — must return 200 and render the English + Korean policy without authentication.
2. Install the release AAB on a device, open Settings → Privacy Policy — OS browser must open the same URL.

### Play Console submission

- Play Console → app → Main store listing → **Privacy policy** → paste `https://studio-mangru.github.io/privacy`.
- The same URL goes into every future Studio Mangru app's Play Console.

### In-app link requirement (already wired)

Google Play also requires a privacy policy link **inside** the app (usually Settings → Privacy or similar). Solitaire Tower already satisfies this:

- `Assets/Prefabs/UI/SettingPanel.prefab` contains a "Privacy Policy" button bound to `SettingPanelView.privacyButton`.
- `OnPrivacyClicked` calls `Application.OpenURL(AppConfig.PrivacyPolicyUrl)`.
- `AppConfig.asset` sets `privacyPolicyUrl` to `https://studio-mangru.github.io/privacy`.

As long as the URL above serves a valid policy, the Play Store requirement is met.

### Future migration to owned domain (optional)

When `mangru.dev` is DNS-wired, move the policy to `https://www.mangru.dev/privacy`:

1. Publish the same content at the new URL (any static host works — GitHub Pages with CNAME, Netlify, Vercel).
2. Update `AppConfig.asset → privacyPolicyUrl` and the Play Console listing URL to the new value.
3. Rebuild + resubmit the AAB only if any other release changes are bundled; updating the Play Console URL alone does not require a rebuild.
4. Keep the old GitHub Pages URL live (as a redirect) for at least 90 days so previously-installed builds still find the policy.

---

## Contact email (decided)

Using **`mangru.studio@gmail.com`** as the single developer-contact email for:

- Play Console developer account
- Privacy Policy (this document)
- In-app "Contact" links
- Crash-report reply-to

All drafts above are already filled in. Set Gmail forwarding → your primary inbox if you want (Settings → Forwarding and POP/IMAP).
