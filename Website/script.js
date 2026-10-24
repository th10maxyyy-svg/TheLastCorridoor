// ==========================================
// THE LAST CORRIDOR — HOLLOWVEIN STUDIOS
// ==========================================

// 1. Preloader Failsafe (works on both local file:// and live web servers)
window.addEventListener('DOMContentLoaded', () => {
    const progressBar = document.querySelector('.loading-progress');
    if (progressBar) {
        setTimeout(() => { progressBar.style.width = '100%'; }, 120);
    }
    setTimeout(() => {
        document.body.classList.add('loaded');
    }, 950);
});

// 2. Custom Cursor Glow
const cursorGlow = document.getElementById('cursor-glow');
document.addEventListener('mousemove', (e) => {
    if (cursorGlow) {
        cursorGlow.style.left = e.clientX + 'px';
        cursorGlow.style.top = e.clientY + 'px';
    }
});

// 3. Scroll Reveal Animation
const observer = new IntersectionObserver((entries, obs) => {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            entry.target.classList.add('is-visible');
            obs.unobserve(entry.target);
        }
    });
}, { threshold: 0.12 });

document.querySelectorAll('.fade-in-section').forEach((section) => {
    observer.observe(section);
});

// 4. 3D Tilt Effect on Cards
document.querySelectorAll('.card-tilt').forEach(el => {
    el.addEventListener('mousemove', (e) => {
        const rect = el.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;
        const rotateX = ((y - rect.height / 2) / (rect.height / 2)) * -6;
        const rotateY = ((x - rect.width / 2) / (rect.width / 2)) * 6;
        el.style.transform = `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) scale3d(1.01, 1.01, 1.01)`;
        el.style.transition = 'transform 0.1s ease';
    });
    el.addEventListener('mouseleave', () => {
        el.style.transform = 'perspective(1000px) rotateX(0) rotateY(0) scale3d(1, 1, 1)';
        el.style.transition = 'transform 0.45s ease';
    });
});

// 5. Direct Download Setup (GitHub Release v1.0)
const DOWNLOAD_URL = 'https://github.com/th10maxyyy-svg/TheLastCorridoor/releases/download/v1.0/TheLastCorridor_Windows.zip';
const downloadLink = document.getElementById('download-link');
if (downloadLink) downloadLink.href = DOWNLOAD_URL;

document.querySelectorAll('a[href="#download"]').forEach(btn => {
    btn.addEventListener('click', (e) => {
        e.preventDefault();
        const dl = document.getElementById('download');
        if (dl) dl.scrollIntoView({ behavior: 'smooth' });
    });
});

// 6. Scroll Progress Bar & Dynamic Heartbeat BPM Readout
const progressBar = document.getElementById('scroll-progress');
const bpmValueEl = document.getElementById('bpm-value');
window.addEventListener('scroll', () => {
    const totalScroll = document.documentElement.scrollTop;
    const windowHeight = Math.max(1, document.documentElement.scrollHeight - document.documentElement.clientHeight);
    const ratio = Math.min(1, Math.max(0, totalScroll / windowHeight));
    if (progressBar) progressBar.style.width = `${ratio * 100}%`;
    if (bpmValueEl) {
        const bpm = Math.round(72 + ratio * 64 + Math.sin(Date.now() * 0.005) * 3);
        bpmValueEl.textContent = bpm;
    }
});

// 7. Interactive Flashlight / Headlamp Mode
const headlampBtn = document.getElementById('headlamp-toggle');
const headlampOverlay = document.getElementById('headlamp-overlay');
let headlampActive = false;

if (headlampBtn && headlampOverlay) {
    headlampBtn.addEventListener('click', () => {
        headlampActive = !headlampActive;
        if (headlampActive) {
            headlampOverlay.classList.add('active');
            headlampBtn.classList.add('active');
            headlampBtn.textContent = 'Flashlight: ON';
            document.body.style.cursor = 'crosshair';
        } else {
            headlampOverlay.classList.remove('active');
            headlampBtn.classList.remove('active');
            headlampBtn.textContent = 'Flashlight: OFF';
            document.body.style.cursor = 'default';
        }
    });

    document.addEventListener('mousemove', (e) => {
        if (headlampActive) {
            headlampOverlay.style.background = `radial-gradient(circle 220px at ${e.clientX}px ${e.clientY}px, transparent 0%, rgba(0,0,0,0.97) 100%)`;
        }
    });
}

// 8. Floating Crimson Embers & Ash Particle Canvas
const emberCanvas = document.getElementById('ember-canvas');
if (emberCanvas) {
    const ctx = emberCanvas.getContext('2d');
    let w = (emberCanvas.width = window.innerWidth);
    let h = (emberCanvas.height = window.innerHeight);

    window.addEventListener('resize', () => {
        w = emberCanvas.width = window.innerWidth;
        h = emberCanvas.height = window.innerHeight;
    });

    const particles = Array.from({ length: 42 }, () => ({
        x: Math.random() * w,
        y: Math.random() * h,
        r: Math.random() * 2.1 + 0.6,
        vx: (Math.random() - 0.5) * 0.35,
        vy: -Math.random() * 0.55 - 0.15,
        alpha: Math.random() * 0.55 + 0.15,
        crimson: Math.random() > 0.35
    }));

    function drawEmbers() {
        ctx.clearRect(0, 0, w, h);
        for (const p of particles) {
            p.x += p.vx;
            p.y += p.vy;
            if (p.y < -10) { p.y = h + 10; p.x = Math.random() * w; }
            if (p.x < -10) p.x = w + 10;
            if (p.x > w + 10) p.x = -10;

            ctx.beginPath();
            ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
            ctx.fillStyle = p.crimson
                ? `rgba(225, 42, 28, ${p.alpha})`
                : `rgba(215, 200, 185, ${p.alpha * 0.5})`;
            ctx.fill();
        }
        requestAnimationFrame(drawEmbers);
    }
    drawEmbers();
}

// 9. Gallery Lightbox Modal
const lightboxModal = document.getElementById('lightbox-modal');
const lightboxImg = document.getElementById('lightbox-img');
const closeLightbox = document.getElementById('close-lightbox');

document.querySelectorAll('.media-item img').forEach(img => {
    img.parentElement.addEventListener('click', () => {
        if (lightboxModal && lightboxImg) {
            lightboxImg.src = img.src;
            lightboxModal.classList.add('show');
        }
    });
});

if (closeLightbox && lightboxModal) {
    closeLightbox.addEventListener('click', () => lightboxModal.classList.remove('show'));
    lightboxModal.addEventListener('click', (e) => {
        if (e.target === lightboxModal) lightboxModal.classList.remove('show');
    });
}

// 10. Camcorder Timecode
const timecodeEl = document.getElementById('timecode');
if (timecodeEl) {
    const startTime = Date.now();
    setInterval(() => {
        const elapsed = Date.now() - startTime;
        const date = new Date(elapsed);
        const hrs = String(date.getUTCHours()).padStart(2, '0');
        const mins = String(date.getUTCMinutes()).padStart(2, '0');
        const secs = String(date.getUTCSeconds()).padStart(2, '0');
        const ms = String(Math.floor(date.getUTCMilliseconds() / 10)).padStart(2, '0');
        timecodeEl.innerText = `REC ${hrs}:${mins}:${secs}:${ms}`;
    }, 50);
}

// 11. Subtle Horror Ambient Audio on Interaction
let audioCtx;
let isAudioInitialized = false;
let droneOsc, droneGain;
let lastHeartbeatTime = 0;

function playCreepySound() {
    if (!audioCtx) return;
    const osc = audioCtx.createOscillator();
    const gainNode = audioCtx.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(42, audioCtx.currentTime);
    osc.frequency.exponentialRampToValueAtTime(12, audioCtx.currentTime + 0.8);
    gainNode.gain.setValueAtTime(0, audioCtx.currentTime);
    gainNode.gain.linearRampToValueAtTime(0.25, audioCtx.currentTime + 0.08);
    gainNode.gain.exponentialRampToValueAtTime(0.01, audioCtx.currentTime + 0.8);
    osc.connect(gainNode);
    gainNode.connect(audioCtx.destination);
    osc.start();
    osc.stop(audioCtx.currentTime + 0.8);
}

document.querySelectorAll('.feature-card, .ending-card, .media-item').forEach(el => {
    el.addEventListener('mouseenter', playCreepySound);
});

function initAmbientAudio() {
    if (isAudioInitialized) return;
    isAudioInitialized = true;
    droneOsc = audioCtx.createOscillator();
    droneOsc.type = 'sine';
    droneOsc.frequency.setValueAtTime(38, audioCtx.currentTime);
    droneGain = audioCtx.createGain();
    droneGain.gain.setValueAtTime(0, audioCtx.currentTime);
    droneGain.gain.linearRampToValueAtTime(0.12, audioCtx.currentTime + 4);
    droneOsc.connect(droneGain);
    droneGain.connect(audioCtx.destination);
    droneOsc.start();
}

function playHeartbeat() {
    if (!isAudioInitialized) return;
    const now = audioCtx.currentTime;
    const osc = audioCtx.createOscillator();
    const gainNode = audioCtx.createGain();
    osc.connect(gainNode);
    gainNode.connect(audioCtx.destination);
    osc.frequency.setValueAtTime(135, now);
    osc.frequency.exponentialRampToValueAtTime(0.01, now + 0.45);
    gainNode.gain.setValueAtTime(0.65, now);
    gainNode.gain.exponentialRampToValueAtTime(0.01, now + 0.45);
    osc.start(now);
    osc.stop(now + 0.45);
}

document.addEventListener('click', () => {
    if (!audioCtx) {
        audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    }
    if (audioCtx.state === 'suspended') {
        audioCtx.resume();
    }
    initAmbientAudio();
}, { once: true });

window.addEventListener('scroll', () => {
    if (!isAudioInitialized) return;
    const scrollPercent = window.scrollY / Math.max(1, document.body.scrollHeight - window.innerHeight);
    const heartbeatInterval = 1.5 - (scrollPercent * 1.1);
    const now = audioCtx.currentTime;
    if (now - lastHeartbeatTime > heartbeatInterval) {
        playHeartbeat();
        setTimeout(playHeartbeat, 150);
        lastHeartbeatTime = now;
    }
    if (droneOsc) droneOsc.frequency.setValueAtTime(38 + (scrollPercent * 18), now);
});
