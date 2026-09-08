// Custom Cursor Logic
const cursorGlow = document.getElementById('cursor-glow');

document.addEventListener('mousemove', (e) => {
    // Update cursor position to follow the mouse
    cursorGlow.style.left = e.clientX + 'px';
    cursorGlow.style.top = e.clientY + 'px';
});

// Scroll Reveal Animation (Fade-in on scroll)
const observerOptions = {
    root: null,
    rootMargin: '0px',
    threshold: 0.15
};

const observer = new IntersectionObserver((entries, observer) => {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            entry.target.classList.add('is-visible');
            observer.unobserve(entry.target);
        }
    });
}, observerOptions);

document.querySelectorAll('.fade-in-section').forEach((section) => {
    observer.observe(section);
});

// 3D Tilt Effect on Cards (Parallax/Hover)
const tiltElements = document.querySelectorAll('.card-tilt');

tiltElements.forEach(el => {
    el.addEventListener('mousemove', (e) => {
        const rect = el.getBoundingClientRect();
        const x = e.clientX - rect.left; // x position within the element
        const y = e.clientY - rect.top;  // y position within the element
        
        const centerX = rect.width / 2;
        const centerY = rect.height / 2;
        
        const rotateX = ((y - centerY) / centerY) * -10; // Max rotation 10deg
        const rotateY = ((x - centerX) / centerX) * 10;
        
        el.style.transform = `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) scale3d(1.02, 1.02, 1.02)`;
        el.style.transition = 'transform 0.1s ease';
    });
    
    el.addEventListener('mouseleave', () => {
        el.style.transform = `perspective(1000px) rotateX(0) rotateY(0) scale3d(1, 1, 1)`;
        el.style.transition = 'transform 0.5s ease';
    });
});

// Glitch effect enhancement (optional random stutter)
const glitchText = document.querySelector('.glitch');
setInterval(() => {
    if (Math.random() > 0.8) {
        glitchText.style.textShadow = `
            ${Math.random() * 10 - 5}px ${Math.random() * 10 - 5}px 0 rgba(255,0,0,0.8),
            ${Math.random() * -10 + 5}px ${Math.random() * -10 + 5}px 0 rgba(0,0,255,0.8)
        `;
        setTimeout(() => {
            glitchText.style.textShadow = '0 0 20px rgba(255, 255, 255, 0.3)';
        }, 100);
    }
}, 2000);

// Coming Soon Modal Logic
const modal = document.getElementById('coming-soon-modal');
const closeBtn = document.querySelector('.close-modal');
const downloadBtns = document.querySelectorAll('a[href="#download"]');

downloadBtns.forEach(btn => {
    btn.addEventListener('click', (e) => {
        e.preventDefault(); // Prevent scrolling
        modal.classList.add('show');
    });
});

closeBtn.addEventListener('click', () => {
    modal.classList.remove('show');
});

// Close modal when clicking outside the box
window.addEventListener('click', (e) => {
    if (e.target === modal) {
        modal.classList.remove('show');
    }
});

// Scroll Progress Bar
const progressBar = document.getElementById('scroll-progress');
window.addEventListener('scroll', () => {
    const totalScroll = document.documentElement.scrollTop;
    const windowHeight = document.documentElement.scrollHeight - document.documentElement.clientHeight;
    const scroll = `${totalScroll / windowHeight * 100}%`;
    progressBar.style.width = scroll;
});
// ==========================================
// THE LAST CORRIDOR - INSANE HORROR UPGRADES
// ==========================================

// 1. Headlamp Mode
const headlampBtn = document.getElementById('headlamp-toggle');
const headlampOverlay = document.getElementById('headlamp-overlay');
let headlampActive = false;

headlampBtn.addEventListener('click', () => {
    headlampActive = !headlampActive;
    if (headlampActive) {
        headlampOverlay.classList.add('active');
        headlampBtn.classList.add('active');
        headlampBtn.innerHTML = '🔦 Headlamp: ON';
        document.body.style.cursor = 'crosshair';
    } else {
        headlampOverlay.classList.remove('active');
        headlampBtn.classList.remove('active');
        headlampBtn.innerHTML = '🔦 Headlamp: OFF';
        document.body.style.cursor = 'default';
    }
});

// Update headlamp position
document.addEventListener('mousemove', (e) => {
    if (headlampActive) {
        // Move the transparent circle of the radial gradient to follow the mouse
        headlampOverlay.style.background = `radial-gradient(circle 200px at ${e.clientX}px ${e.clientY}px, transparent 0%, rgba(0,0,0,0.98) 100%)`;
    }
});

// 2. Web Audio API - Creepy Heartbeat/Drone
let audioCtx;
function playCreepySound() {
    if (!audioCtx) {
        audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    }
    
    // Create oscillator for low frequency drone
    const osc = audioCtx.createOscillator();
    const gainNode = audioCtx.createGain();
    
    osc.type = 'sine';
    osc.frequency.setValueAtTime(40, audioCtx.currentTime); // Deep bass
    osc.frequency.exponentialRampToValueAtTime(10, audioCtx.currentTime + 1);
    
    // Volume envelope
    gainNode.gain.setValueAtTime(0, audioCtx.currentTime);
    gainNode.gain.linearRampToValueAtTime(0.5, audioCtx.currentTime + 0.1);
    gainNode.gain.exponentialRampToValueAtTime(0.01, audioCtx.currentTime + 1);
    
    osc.connect(gainNode);
    gainNode.connect(audioCtx.destination);
    
    osc.start();
    osc.stop(audioCtx.currentTime + 1);
}

// Add sound to feature cards on hover
const spookyElements = document.querySelectorAll('.feature-card, .media-item');
spookyElements.forEach(el => {
    el.addEventListener('mouseenter', () => {
        playCreepySound();
    });
});

// 3. Random Room Flicker
setInterval(() => {
    // 5% chance every 3 seconds to trigger a flicker
    if (Math.random() < 0.05) {
        document.body.classList.add('flicker-active');
        playCreepySound();
        setTimeout(() => {
            document.body.classList.remove('flicker-active');
        }, 500);
    }
}, 3000);

// ==========================================
// ULTRA-PREMIUM HORROR HUD & AUDIO
// ==========================================

// 1. Camcorder Timecode
const timecodeEl = document.getElementById('timecode');
if (timecodeEl) {
    let startTime = Date.now();
    setInterval(() => {
        const elapsed = Date.now() - startTime;
        const date = new Date(elapsed);
        const hrs = String(date.getUTCHours()).padStart(2, '0');
        const mins = String(date.getUTCMinutes()).padStart(2, '0');
        const secs = String(date.getUTCSeconds()).padStart(2, '0');
        const ms = String(Math.floor(date.getUTCMilliseconds() / 10)).padStart(2, '0');
        timecodeEl.innerText = `${hrs}:${mins}:${secs}:${ms}`;
    }, 40); // 25fps update rate
}

// 2. Drone and Heartbeat Audio
let isAudioInitialized = false;
let droneOsc, droneGain;
let lastHeartbeatTime = 0;

function initAmbientAudio() {
    if (isAudioInitialized) return;
    isAudioInitialized = true;

    // Dark Drone
    droneOsc = audioCtx.createOscillator();
    droneOsc.type = 'sine';
    droneOsc.frequency.setValueAtTime(40, audioCtx.currentTime); // Low rumble

    droneGain = audioCtx.createGain();
    droneGain.gain.setValueAtTime(0, audioCtx.currentTime);
    droneGain.gain.linearRampToValueAtTime(0.2, audioCtx.currentTime + 5); // Fade in over 5s

    droneOsc.connect(droneGain);
    droneGain.connect(audioCtx.destination);
    droneOsc.start();
}

function playHeartbeat() {
    if (!isAudioInitialized) return;
    const now = audioCtx.currentTime;
    
    // Create kick drum heartbeat sound
    const osc = audioCtx.createOscillator();
    const gainNode = audioCtx.createGain();
    
    osc.connect(gainNode);
    gainNode.connect(audioCtx.destination);
    
    osc.frequency.setValueAtTime(150, now);
    osc.frequency.exponentialRampToValueAtTime(0.01, now + 0.5);
    
    gainNode.gain.setValueAtTime(1, now);
    gainNode.gain.exponentialRampToValueAtTime(0.01, now + 0.5);
    
    osc.start(now);
    osc.stop(now + 0.5);
}

// Start audio on first click
document.addEventListener('click', () => {
    if (!audioCtx) {
        audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    }
    if (audioCtx.state === 'suspended') {
        audioCtx.resume();
    }
    initAmbientAudio();
}, { once: true });

// Heartbeat mapped to scroll
window.addEventListener('scroll', () => {
    if (!isAudioInitialized) return;
    
    // The further down you scroll, the faster the heartbeat
    const scrollPercent = window.scrollY / (document.body.scrollHeight - window.innerHeight);
    
    // Base interval 1.5s, fastest 0.3s
    const heartbeatInterval = 1.5 - (scrollPercent * 1.2); 
    
    const now = audioCtx.currentTime;
    if (now - lastHeartbeatTime > heartbeatInterval) {
        playHeartbeat();
        // Double beat effect
        setTimeout(playHeartbeat, 150);
        lastHeartbeatTime = now;
    }
    
    // Drone gets slightly louder/higher pitched when scrolling down
    droneOsc.frequency.setValueAtTime(40 + (scrollPercent * 20), now);
});
