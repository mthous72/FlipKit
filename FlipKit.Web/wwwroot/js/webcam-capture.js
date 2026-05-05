// Browser-side webcam capture (Roadmap #2 / Docs/27-WEBCAM-CAPTURE-PLAN.md).
//
// Drives the #webcamCaptureModal partial. Each page that needs capture calls
// FlipKitWebcam.attach(triggerSelector, options) — clicks on the trigger open
// the modal, getUserMedia streams into the <video>, the user clicks Capture,
// the JPEG blob POSTs to /api/cards/upload-image, and options.onCapture
// receives { path, url } so the page can stuff them into hidden fields.
//
// Hides itself gracefully: if navigator.mediaDevices is undefined (insecure
// origin / unsupported browser) or enumerateDevices() returns no video
// inputs, the trigger buttons are hidden via [data-webcam-required].

(function (window, document) {
    'use strict';

    const STORAGE_KEY = 'flipkit.preferredCameraId';

    let modalEl, videoEl, canvasEl, deviceSelectEl, statusEl, errorEl;
    let captureBtn, retakeBtn, useThisBtn, cancelBtn;
    let bsModal;
    let activeStream = null;
    let capturedBlob = null;
    let pendingCallback = null;

    function $(sel) { return modalEl ? modalEl.querySelector(sel) : null; }

    function setStatus(msg) {
        if (statusEl) {
            statusEl.textContent = msg || '';
            statusEl.style.display = msg ? 'block' : 'none';
        }
    }

    function setError(msg) {
        if (errorEl) {
            errorEl.textContent = msg || '';
            errorEl.style.display = msg ? 'block' : 'none';
        }
    }

    function enterPreviewMode() {
        capturedBlob = null;
        if (videoEl) videoEl.style.display = 'block';
        if (canvasEl) canvasEl.style.display = 'none';
        if (captureBtn) captureBtn.style.display = '';
        if (retakeBtn) retakeBtn.style.display = 'none';
        if (useThisBtn) useThisBtn.style.display = 'none';
        setError('');
    }

    function enterReviewMode() {
        if (videoEl) videoEl.style.display = 'none';
        if (canvasEl) canvasEl.style.display = 'block';
        if (captureBtn) captureBtn.style.display = 'none';
        if (retakeBtn) retakeBtn.style.display = '';
        if (useThisBtn) useThisBtn.style.display = '';
    }

    async function populateDeviceList() {
        if (!deviceSelectEl) return [];
        deviceSelectEl.innerHTML = '';
        try {
            const all = await navigator.mediaDevices.enumerateDevices();
            const cams = all.filter(d => d.kind === 'videoinput');
            const saved = window.localStorage.getItem(STORAGE_KEY);
            cams.forEach((cam, idx) => {
                const opt = document.createElement('option');
                opt.value = cam.deviceId;
                opt.textContent = cam.label || `Camera ${idx + 1}`;
                if (cam.deviceId === saved) opt.selected = true;
                deviceSelectEl.appendChild(opt);
            });
            return cams;
        } catch (err) {
            setError('Could not enumerate cameras: ' + err.message);
            return [];
        }
    }

    async function startStream(deviceId) {
        await stopStream();
        try {
            setStatus('Opening camera…');
            const constraints = {
                audio: false,
                video: deviceId
                    ? { deviceId: { exact: deviceId }, width: { ideal: 4096 }, height: { ideal: 2160 } }
                    : { width: { ideal: 4096 }, height: { ideal: 2160 } }
            };
            activeStream = await navigator.mediaDevices.getUserMedia(constraints);
            videoEl.srcObject = activeStream;
            await videoEl.play();
            setStatus('');

            // After the first successful getUserMedia call, device labels become
            // available. Repopulate so the picker shows real names instead of
            // "Camera 1" placeholders.
            await populateDeviceList();
        } catch (err) {
            setStatus('');
            if (err.name === 'NotAllowedError')
                setError('Camera permission denied. Allow access in your browser and try again.');
            else if (err.name === 'NotFoundError')
                setError('No camera found.');
            else
                setError('Could not open camera: ' + err.message);
        }
    }

    async function stopStream() {
        if (videoEl) videoEl.pause?.();
        if (activeStream) {
            activeStream.getTracks().forEach(t => t.stop());
            activeStream = null;
        }
    }

    function captureFrame() {
        if (!videoEl || !canvasEl || !videoEl.videoWidth) {
            setError('Preview not ready yet — wait a moment and try again.');
            return;
        }

        canvasEl.width = videoEl.videoWidth;
        canvasEl.height = videoEl.videoHeight;
        const ctx = canvasEl.getContext('2d');
        ctx.drawImage(videoEl, 0, 0, canvasEl.width, canvasEl.height);

        canvasEl.toBlob(blob => {
            if (!blob) {
                setError('Failed to encode capture as JPEG.');
                return;
            }
            capturedBlob = blob;
            enterReviewMode();
        }, 'image/jpeg', 0.92);
    }

    async function uploadAndClose() {
        if (!capturedBlob) return;

        try {
            setStatus('Uploading…');
            const fd = new FormData();
            fd.append('blob', capturedBlob, 'webcam.jpg');

            // Honour ASP.NET's [ValidateAntiForgeryToken] when present on the
            // calling page. Read the hidden token issued by @Html.AntiForgeryToken().
            const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
            const headers = tokenInput
                ? { 'RequestVerificationToken': tokenInput.value }
                : {};

            const resp = await fetch('/api/cards/upload-image', {
                method: 'POST',
                body: fd,
                headers: headers
            });
            if (!resp.ok) {
                const text = await resp.text();
                throw new Error(`HTTP ${resp.status}: ${text}`);
            }

            const json = await resp.json();
            setStatus('');

            // Persist the device the user used so the next open defaults to it.
            if (deviceSelectEl?.value)
                window.localStorage.setItem(STORAGE_KEY, deviceSelectEl.value);

            const cb = pendingCallback;
            pendingCallback = null;
            bsModal.hide();
            if (cb) cb(json);
        } catch (err) {
            setStatus('');
            setError('Upload failed: ' + err.message);
        }
    }

    function ensureBound() {
        if (modalEl) return true;
        modalEl = document.getElementById('webcamCaptureModal');
        if (!modalEl) return false;

        videoEl = $('#webcamVideo');
        canvasEl = $('#webcamCanvas');
        deviceSelectEl = $('#webcamDeviceSelect');
        statusEl = $('#webcamStatus');
        errorEl = $('#webcamError');
        captureBtn = $('#webcamCaptureBtn');
        retakeBtn = $('#webcamRetakeBtn');
        useThisBtn = $('#webcamUseBtn');
        cancelBtn = $('#webcamCancelBtn');

        bsModal = new bootstrap.Modal(modalEl);

        captureBtn?.addEventListener('click', captureFrame);
        retakeBtn?.addEventListener('click', enterPreviewMode);
        useThisBtn?.addEventListener('click', uploadAndClose);
        deviceSelectEl?.addEventListener('change', () => startStream(deviceSelectEl.value));

        modalEl.addEventListener('hidden.bs.modal', async () => {
            await stopStream();
            capturedBlob = null;
            // If the user dismissed via X / Cancel without "Use this" we still
            // need to clear the pending callback so a stale one can't fire.
            pendingCallback = null;
        });

        return true;
    }

    async function openCapture(callback) {
        if (!ensureBound()) {
            console.warn('webcam-capture: modal markup #webcamCaptureModal not found on page.');
            return;
        }

        pendingCallback = callback;
        enterPreviewMode();
        bsModal.show();

        const cams = await populateDeviceList();
        if (cams.length === 0) {
            setError('No cameras detected.');
            return;
        }
        await startStream(deviceSelectEl?.value || cams[0].deviceId);
    }

    async function detectSupport() {
        // navigator.mediaDevices is undefined on insecure origins (anything
        // except localhost or HTTPS). enumerateDevices throws on some browsers
        // when not allowed at all. Either way, hide the trigger buttons.
        if (!navigator.mediaDevices || typeof navigator.mediaDevices.enumerateDevices !== 'function')
            return false;

        try {
            const devs = await navigator.mediaDevices.enumerateDevices();
            return devs.some(d => d.kind === 'videoinput');
        } catch {
            return false;
        }
    }

    function attach(triggerSelector, options) {
        const triggers = document.querySelectorAll(triggerSelector);
        if (triggers.length === 0) return;
        triggers.forEach(btn => {
            btn.addEventListener('click', evt => {
                evt.preventDefault();
                openCapture(result => {
                    if (typeof options?.onCapture === 'function')
                        options.onCapture(result, btn);
                });
            });
        });
    }

    async function gateTriggers(triggerSelector) {
        const supported = await detectSupport();
        const triggers = document.querySelectorAll(triggerSelector);
        if (!supported) {
            triggers.forEach(btn => btn.style.display = 'none');
            const banners = document.querySelectorAll('[data-webcam-unsupported]');
            banners.forEach(el => el.style.display = '');
        }
    }

    window.FlipKitWebcam = {
        attach: attach,
        gate: gateTriggers,
        open: openCapture,
    };
})(window, document);
