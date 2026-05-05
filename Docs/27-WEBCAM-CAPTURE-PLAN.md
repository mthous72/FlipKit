# Webcam Capture Implementation Plan — Desktop + Web

> **Status:** ✅ **Shipped 2026-05-04** on branch `feature/webcam-capture` (4 PRs + 1 follow-up). Branched off `master` ahead of the planned CSV-export prereq because that branch had stalled and the touched files don't overlap. See the Outcome section at the bottom of this doc for what landed vs. what was planned, smoke-test findings, and follow-ups.

## 1. Goal

Let users capture card images directly from a laptop's built-in (or USB) camera, instead of needing a phone-uploaded image, in both the Desktop app (Avalonia) and Web app (browser).

The captured image must drop straight into the existing `ImagePath{N}` flow — meaning the capture step ends with a JPG saved to disk (Desktop) or POSTed to a server endpoint (Web). No exporter, ImgBB, or scanner-pipeline code changes.

## 2. Non-goals

- Video recording — stills only.
- Auto-cropping / background removal — out of scope; user frames the card.
- Replacing phone capture — the existing phone-upload QR flow stays.
- Bulk capture — single-shot per slot for v1.

## 3. Decisions locked in

| Decision | Choice | Rationale |
|---|---|---|
| Desktop capture lib | **OpenCvSharp4** | Cross-platform (Win/Mac/Linux), single dep, full resolution control, JPG-ready output |
| Web capture API | **`navigator.mediaDevices.getUserMedia`** + `<canvas>` | Standard, plugin-free, works in all evergreen browsers |
| Branch | **`feature/webcam-capture`**, off `master` after CSV export merges | Keeps PR scope clean |
| Device selection | **Picker in Settings** (and inline picker in capture dialog) | Laptops often have 2-3 cameras (built-in + external + virtual); picking matters |
| Resolution | **Highest the device reports** (no preset menu) | Card photos benefit from max detail; cheaper than building a resolution UI |

## 4. Architecture

### 4.1 New module layout

```
FlipKit.Core/
└── Services/
    ├── Interfaces/
    │   └── ICameraService.cs               [new — abstract over Desktop/Web capture]
    └── (no Core impl — capture is platform-specific)

FlipKit.Desktop/
├── Services/
│   └── OpenCvCameraService.cs              [new — implements ICameraService via OpenCvSharp4]
├── ViewModels/
│   └── WebcamCaptureViewModel.cs           [new — preview loop, capture, retake, accept]
└── Views/
    └── WebcamCaptureWindow.axaml           [new — modal capture dialog with live preview]

FlipKit.Web/
├── Controllers/
│   └── ImageUploadController.cs            [new — POST /api/cards/upload-image]
├── Views/Shared/
│   └── _WebcamCaptureModal.cshtml          [new — getUserMedia + canvas markup]
└── wwwroot/js/
    └── webcam-capture.js                   [new — getUserMedia, frame grab, blob POST]
```

### 4.2 ICameraService contract

```csharp
public interface ICameraService
{
    /// <summary>Enumerates connected camera devices. Index = OpenCV device index.</summary>
    Task<IReadOnlyList<CameraDevice>> ListDevicesAsync();

    /// <summary>Opens the device, returns a session that yields preview frames and captures stills.</summary>
    Task<ICameraSession> OpenAsync(int deviceIndex, CancellationToken ct = default);
}

public record CameraDevice(int Index, string Name, int MaxWidth, int MaxHeight);

public interface ICameraSession : IAsyncDisposable
{
    /// <summary>Pull the next frame for preview rendering. Returns RGB byte buffer + dims.</summary>
    Task<CapturedFrame?> ReadFrameAsync(CancellationToken ct = default);

    /// <summary>Grab a single high-resolution still and write it to disk as JPG. Returns the path.</summary>
    Task<string> CaptureStillAsync(string outputDir, CancellationToken ct = default);
}
```

The Web side does not implement `ICameraService` (browser owns the camera). Instead, the controller exposes `POST /api/cards/upload-image` that accepts a multipart form with the JPG blob and returns the saved path. The JS layer (`webcam-capture.js`) drives the browser-side capture and POST.

### 4.3 Desktop capture flow

1. User clicks "📷 Capture from Webcam" button next to "Browse..." on Scan or Edit view.
2. `WebcamCaptureWindow` opens as a modal dialog.
3. ViewModel constructor calls `ICameraService.ListDevicesAsync()` → populates a device picker (defaults to the device saved in `AppSettings.PreferredCameraIndex`).
4. User picks a device → ViewModel calls `OpenAsync(index)`, starts a preview loop that calls `ReadFrameAsync` on a background task and pushes RGB byte buffers into an Avalonia `WriteableBitmap` bound to an `<Image>` in the view.
5. User clicks "Capture" → ViewModel calls `CaptureStillAsync(tempDir)` → returns the file path.
6. User reviews the still → clicks "Use This" (returns path to caller via dialog result) or "Retake" (resumes preview).
7. Caller (ScanView or EditCardView) drops the returned path into the appropriate slot.

### 4.4 Web capture flow

1. User clicks "📷 Capture from Webcam" on an inventory or scan page.
2. JS calls `navigator.mediaDevices.enumerateDevices()` → populates a `<select>` of video input devices (defaults to `localStorage.preferredCameraId`).
3. User picks → JS calls `getUserMedia({ video: { deviceId: { exact: id } }, audio: false })` → attaches stream to a `<video>` element inside the modal.
4. User clicks "Capture" → JS draws the current `<video>` frame onto a `<canvas>`, calls `canvas.toBlob('image/jpeg', 0.92)`, POSTs the blob to `/api/cards/upload-image` with multipart form.
5. Server saves blob to `wwwroot/uploads/` (or LocalAppData), returns `{ path: "..." }`.
6. JS sets the returned path on the appropriate hidden input (slot 1-8) and updates the thumbnail preview.

## 5. Settings additions

| Setting | Default | Purpose |
|---|---|---|
| `PreferredCameraIndex` | `0` (or null) | Desktop default camera; `OpenCvSharp4` device index |
| `PreferredCameraName` | `null` | Cross-OS device-name hint (used to fall back when the index changes between sessions) |
| `WebcamCaptureEnabled` | `true` | Hide the buttons on machines without cameras (auto-flipped if `ListDevicesAsync` returns empty) |

Browser side stores `preferredCameraId` in `localStorage` only — server doesn't need to know per-user camera prefs.

## 6. Permissions

**Desktop:**
- Windows: no consent dialog for OpenCvSharp4 access (it goes through DirectShow/Media Foundation which is unprompted for desktop apps). LED indicator on most laptops covers user awareness.
- Mac: macOS shows a system camera-permission prompt the first time; we need `NSCameraUsageDescription` in `Info.plist` if/when we ship a packaged Mac build. Avalonia bundles get this via the project's bundle config.
- Linux: V4L2 access is unprompted; user just needs read perms on `/dev/video0`.

**Web:**
- Browsers prompt automatically on first `getUserMedia` call. The site must be served over HTTPS (or localhost) — `getUserMedia` returns `NotAllowedError` on HTTP except for localhost. FlipKit Web runs on localhost in Hub mode and on Tailscale IPs (HTTP) for remote — Tailscale-mode webcam capture **will not work** without TLS or browser-flag overrides. Documenting as a known limitation; users on remote-mode can fall back to phone upload.

## 7. UI integration points

### Scan view (Desktop)
- Add a "📷 Webcam" button next to the existing "Browse..." button on the Front Image card.
- Add the same button on the Back Image card.
- Add the same button on the "+ Add Photo" Additional Photos panel header (becomes "+ Add Photo ▼" with a flyout: "From file..." or "From webcam...").

### Edit card view (Desktop)
- Same pattern — webcam button on each existing slot's "Replace" action and on the Additional Photos add row.

### Scan / Inventory view (Web)
- Same pattern with a `<button class="capture-webcam">` opening the modal. Hidden when `navigator.mediaDevices` is undefined or `enumerateDevices()` returns no video inputs.

## 8. Dependencies

| Package | Version | Where |
|---|---|---|
| `OpenCvSharp4` | 4.11.x | FlipKit.Desktop |
| `OpenCvSharp4.runtime.win` | 4.11.x | FlipKit.Desktop (Windows) |
| `OpenCvSharp4.runtime.osx` | 4.11.x | FlipKit.Desktop (Mac) |
| `OpenCvSharp4.runtime.ubuntu.20.04-x64` | 4.11.x | FlipKit.Desktop (Linux) |

The runtime packages add ~50MB per platform to the published artifact. Acceptable trade for cross-platform camera support without writing 3 native shims.

Web side adds zero packages — `getUserMedia` is browser-native.

## 9. Testing strategy

- **Smoke**: open the capture dialog with a real laptop webcam, verify preview renders and capture writes a JPG of the device's max resolution.
- **No-device path**: unplug all cameras, verify `ListDevicesAsync` returns empty and the buttons hide / show "No camera found".
- **Permission denied (Mac)**: deny camera access on first launch, verify a clear error message rather than a crash.
- **Multiple cameras**: connect a USB webcam alongside the built-in, verify the picker shows both and switching works without restart.
- **Resolution sanity**: confirm captured JPG dimensions match the device's reported max (most laptops: 1280×720 or 1920×1080).
- **Web HTTPS gate**: serve over plain HTTP from a non-localhost IP, verify the button is disabled or shows a clear "HTTPS required" message.
- **Round-trip**: capture → save card → export → verify the path flows through to `ImagePath{N}` → ImgBB upload → CSV row.

## 10. Implementation order

1. Add `OpenCvSharp4` deps to `FlipKit.Desktop.csproj`.
2. Define `ICameraService` + `CameraDevice` + `ICameraSession` + `CapturedFrame` records in `FlipKit.Core`.
3. Implement `OpenCvCameraService` in `FlipKit.Desktop`.
4. Build `WebcamCaptureViewModel` + `WebcamCaptureWindow` (preview loop + capture + retake + accept).
5. Wire "📷 Webcam" buttons into ScanView (front, back, additional).
6. Wire same buttons into EditCardView.
7. Add `PreferredCameraIndex` / `PreferredCameraName` to `AppSettings` + a Settings UI device picker.
8. Web: build `webcam-capture.js` + the modal partial.
9. Web: add `ImageUploadController` with `POST /api/cards/upload-image`.
10. Web: wire the modal into Scan / Inventory templates.
11. Manual smoke pass on Windows (primary), then macOS and Linux if available.
12. Document the HTTPS limitation for Tailscale-mode in README + Web user guide.

## 11. Risks / open questions

- **OpenCvSharp4 on M-series Macs**: the `osx` runtime package may not be ARM64-native. If the user targets Apple Silicon, we may need `OpenCvSharp4.runtime.osx-arm64` or a Rosetta fallback. Test on M1/M2 before declaring Mac support.
- **Avalonia preview-frame perf**: pushing 30fps RGB byte buffers into `WriteableBitmap` may stutter on lower-end hardware. If we see this, drop preview to ~15fps via a frame skipper.
- **Web HTTPS limitation**: Tailscale-mode users (the primary remote-mode case) can't use webcam without TLS. Either accept the limitation or stand up a self-signed cert flow — defer to a follow-up if it bites users.
- **Driver quirks**: some virtual cameras (OBS, Snap Camera) appear as devices but don't respond to all OpenCV calls. Add a "test capture" button in Settings to validate before saving the preference.

---

## 12. Outcome (post-ship notes)

Shipped on 2026-05-04 across 5 commits on `feature/webcam-capture`, merged to `master`:

| Commit | Layer | What |
|---|---|---|
| PR 1 | Desktop | `ICameraService` / `ICameraSession` / `CameraDevice` / `CapturedFrame` in `FlipKit.Core`; `OpenCvCameraService` + `WebcamCaptureViewModel` + `WebcamCaptureWindow` in Desktop |
| PR 2 | Desktop | `IWebcamCaptureDialogService` abstraction; ScanView and EditCardView 📷 button wiring; DI registration. Added a Replace flow on EditCardView since one didn't exist before. |
| PR 2.1 | Desktop | Smoke-test fixes — see "Findings on first hardware run" below |
| PR 3 | Desktop | `AppSettings.WebcamCaptureEnabled / PreferredCameraIndex / PreferredCameraName`; Settings UI device picker + master toggle; `IsWebcamEnabled` visibility gate on all 7 webcam buttons |
| PR 4 | Web | `ImageUploadController` POST endpoint; `webcam-capture.js` (browser `getUserMedia` + canvas + blob upload); `_WebcamCaptureModal.cshtml` partial; Scan/Index wiring; `ScanController.Upload` accepts path-as-alternative-to-file |

**Findings on first hardware run (PR 2.1):**

- **NVIDIA Broadcast virtual camera** showed up alongside the real webcam at 640×480, and the original "Camera 0" / "Camera 1" labels were indistinguishable. Fix: probe each device with a max-res request and label as `Camera N — WxH` so users can spot the low-res virtual cam.
- **First captures looked blurry in the ScanView preview pane** even though the underlying JPG was 1920×1080. Two unrelated issues:
  1. ScanView used `FilePathToBitmapConverter` (a 56-px DataGrid thumbnail decoder) for the main 300-px preview surface. Switched to `FilePathToFullSizeBitmapConverter` (which EditCardView already used).
  2. Avalonia's default `BitmapInterpolationMode` is `Low` (bilinear) — added `RenderOptions.BitmapInterpolationMode="HighQuality"` on the preview Image controls.
- **Captured stills came out of focus on first take** because the OpenCV driver buffer holds 4-5 frames, so the still grab returned a frame from before continuous-autofocus settled after the preview pause. Fixes: `CAP_PROP_BUFFERSIZE=1` in `OpenAsync` (drivers that ignore it fall through to the drain loop), 15-frame / ~750 ms drain in `CaptureStillAsync`, JPG quality 92 → 95, "Holding still — waiting for autofocus…" status hint.
- **Diagnostic log line** — every capture now writes `Webcam still captured: WxH, NN KB → path` at INF so future "blurry capture" reports can be triaged from the log.

**Deviations from the plan:**

- **Branched ahead of CSV-export merge.** The plan called for `feature/webcam-capture` off `master` *after* `feature/csv-export-overhaul` merged. That branch had stalled, so we branched off `master` directly. The two paths touch different files (CSV exporters vs. ScanViewModel image acquisition) so the conflict risk was zero.
- **Device labels are `Camera N — WxH`, not OS-friendly names.** OpenCvSharp4 has no cross-platform device-name API. WMI on Windows could provide friendly names but adds the `System.Management` dep — deferred. The resolution suffix turned out to be the more useful disambiguator anyway.
- **EditCardView Replace flow was added in PR 2.** Plan said "webcam button on each existing slot's Replace action" — but no Replace action existed yet. Added Browse + Webcam together (the same input/output pair), plus the matching `_originalCard.ImagePathFront/Back` write-through on save and ImageUrl1/2 cache invalidation when the local file changes.

**Deferred / not shipped:**

- **Inventory edit-card webcam wiring on Web.** The Desktop EditCardView got the Replace flow, but the Web Inventory edit page didn't. Lower priority because the primary Web user flow is Scan, not Edit.
- **Mac/Linux smoke pass.** Only Windows was verified on real hardware. The OpenCvSharp4 OSX/Linux runtime packages are wired into the csproj but untested.
- **OpenCvSharp4 osx-arm64 risk** flagged in §11 — still untested on Apple Silicon.
- **Test capture button validation** flagged in §11 — replaced by the Settings → "Test capture…" button which exercises the full dialog instead.

*Original plan content above is preserved for historical reference. Current behavior may diverge — check the code.*
