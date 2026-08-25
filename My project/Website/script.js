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
