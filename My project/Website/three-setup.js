import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

// 1. Setup Scene, Camera, and Renderer
const canvas = document.getElementById('webgl-canvas');
const scene = new THREE.Scene();
// Fog to blend the model into the dark background
scene.fog = new THREE.FogExp2(0x0a0a0a, 0.02);

const camera = new THREE.PerspectiveCamera(75, window.innerWidth / window.innerHeight, 0.1, 1000);
camera.position.z = 10;
camera.position.y = 2;

const renderer = new THREE.WebGLRenderer({ canvas: canvas, alpha: true, antialias: true });
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.setPixelRatio(window.devicePixelRatio);
// Important for GLTF PBR materials
renderer.outputColorSpace = THREE.SRGBColorSpace; 

// 2. Lighting (Horror Vibe)
const ambientLight = new THREE.AmbientLight(0xffffff, 2.0); // Brighter light to ensure visibility
scene.add(ambientLight);

const redRimLight = new THREE.PointLight(0xff2a00, 10, 100);
redRimLight.position.set(10, 10, -10);
scene.add(redRimLight);

const blueFillLight = new THREE.PointLight(0x1a3a5a, 5, 100);
blueFillLight.position.set(-10, 5, 10);
scene.add(blueFillLight);

const frontLight = new THREE.DirectionalLight(0xffffff, 1.5);
frontLight.position.set(0, 0, 10);
scene.add(frontLight);

// 3. Load GLTF Model
let statueModel = null;
let targetRotationX = 0;
let targetRotationY = Math.PI / 4;
let mouseX = 0;
let mouseY = 0;

// Mouse Tracking for Parallax
document.addEventListener('mousemove', (event) => {
    // Normalize mouse position from -1 to 1
    mouseX = (event.clientX / window.innerWidth) * 2 - 1;
    mouseY = -(event.clientY / window.innerHeight) * 2 + 1;
});

const gltfLoader = new GLTFLoader();
gltfLoader.load(
    'models/scene.gltf',
    (gltf) => {
        const object = gltf.scene;
        
        // Auto-Center and Auto-Scale the model to guarantee visibility
        const box = new THREE.Box3().setFromObject(object);
        const center = box.getCenter(new THREE.Vector3());
        const size = box.getSize(new THREE.Vector3());

        // Target height of 15 units
        const maxDim = Math.max(size.x, size.y, size.z);
        const scale = 15 / maxDim;
        object.scale.set(scale, scale, scale);

        // Center the object in front of the camera
        object.position.x = -center.x * scale;
        object.position.y = -center.y * scale - 2; // Offset slightly down
        object.position.z = -center.z * scale - 5; // Place in front of camera
        
        object.rotation.y = Math.PI / 4; // Angled slightly
        
        // Store the base position to use in animation
        object.userData.baseY = object.position.y;
        object.userData.baseZ = object.position.z;
        object.userData.baseRotY = Math.PI / 4;

        statueModel = object;
        scene.add(object);

        // Hide Preloader once loaded
        setTimeout(() => {
            document.body.classList.add('loaded');
        }, 500); // Small delay for dramatic effect
    },
    (xhr) => {
        const progress = (xhr.loaded / xhr.total) * 100;
        const progressBar = document.querySelector('.loading-progress');
        if (progressBar) progressBar.style.width = progress + '%';
    },
    (error) => {
        console.error('An error happened while loading the GLTF', error);
    }
);

// 4. Scroll Animation Logic
let currentScrollY = window.scrollY;

window.addEventListener('scroll', () => {
    currentScrollY = window.scrollY;
});

// 5. Render Loop
function animate() {
    requestAnimationFrame(animate);

    if (statueModel) {
        // Map mouse position to target rotation (Parallax effect)
        targetRotationY = statueModel.userData.baseRotY + mouseX * 0.5;
        targetRotationX = -mouseY * 0.2;

        // Smoothly interpolate rotation towards the target (creepy tracking)
        statueModel.rotation.y += (targetRotationY - statueModel.rotation.y) * 0.05;
        statueModel.rotation.x += (targetRotationX - statueModel.rotation.x) * 0.05;
        
        // Map scroll position to statue's Y position and scale (Exploded View effect)
        // As you scroll down, the statue rises and comes closer
        const scrollFactor = currentScrollY * 0.005;
        
        // Lerp for smooth transition using the dynamically calculated base positions
        const targetY = statueModel.userData.baseY + scrollFactor * 2;
        const targetZ = statueModel.userData.baseZ + scrollFactor * 3;

        statueModel.position.y += (targetY - statueModel.position.y) * 0.05;
        statueModel.position.z += (targetZ - statueModel.position.z) * 0.05;
    }

    renderer.render(scene, camera);
}

animate();

// Handle Window Resize
window.addEventListener('resize', () => {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
});
