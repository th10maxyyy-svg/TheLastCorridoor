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

// Floating Ash Particles (Canvas)
const canvas = document.getElementById('particle-canvas');
const ctx = canvas.getContext('2d');
canvas.width = window.innerWidth;
canvas.height = window.innerHeight;

const particlesArray = [];
const numberOfParticles = 100;

class Particle {
    constructor() {
        this.x = Math.random() * canvas.width;
        this.y = Math.random() * canvas.height;
        this.size = Math.random() * 2 + 0.5;
        this.speedX = Math.random() * 1 - 0.5;
        this.speedY = Math.random() * 1.5 + 0.5;
        this.opacity = Math.random() * 0.5 + 0.1;
    }
    update() {
        this.y -= this.speedY;
        this.x += this.speedX;
        
        // Reset particle if it goes off screen
        if (this.y < 0) {
            this.y = canvas.height;
            this.x = Math.random() * canvas.width;
        }
    }
    draw() {
        ctx.fillStyle = `rgba(200, 200, 200, ${this.opacity})`;
        ctx.beginPath();
        ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
        ctx.fill();
    }
}

function initParticles() {
    for (let i = 0; i < numberOfParticles; i++) {
        particlesArray.push(new Particle());
    }
}

function animateParticles() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    for (let i = 0; i < particlesArray.length; i++) {
        particlesArray[i].update();
        particlesArray[i].draw();
    }
    requestAnimationFrame(animateParticles);
}

initParticles();
animateParticles();

window.addEventListener('resize', () => {
    canvas.width = window.innerWidth;
    canvas.height = window.innerHeight;
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

// 4. Video Auto-Play on Scroll (Intersection Observer)
const gameplayVideo = document.getElementById('gameplay-video');
if (gameplayVideo) {
    const videoObserver = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                // Video is visible, play it
                gameplayVideo.play().catch(e => console.log("Auto-play blocked", e));
            } else {
                // Video is not visible, pause it
                gameplayVideo.pause();
            }
        });
    }, { threshold: 0.3 }); // Play when at least 30% of the video is visible

    videoObserver.observe(gameplayVideo);
}
