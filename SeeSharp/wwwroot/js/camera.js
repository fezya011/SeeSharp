let videoElement = null;
let stream = null;
let dotNetRef = null;
let animationId = null;
let isDetecting = false;

console.log("camera.js loaded");

window.setDotNetReference = (reference) => {
    console.log("setDotNetReference called");
    dotNetRef = reference;
};

window.startCamera = async () => {
    console.log("startCamera called");
    videoElement = document.getElementById('video');
    if (!videoElement) {
        console.error("Video element not found");
        return false;
    }

    try {
        stream = await navigator.mediaDevices.getUserMedia({
            video: { width: 640, height: 480 }
        });
        videoElement.srcObject = stream;
        await videoElement.play();
        console.log("Camera started");
        return true;
    } catch (err) {
        console.error("Camera error:", err);
        return false;
    }
};

window.startRealTimeDetection = () => {
    console.log("startRealTimeDetection called");
    if (animationId) cancelAnimationFrame(animationId);
    isDetecting = true;
    detectLoop();
};

function detectLoop() {
    if (!isDetecting) return;
    
    if (videoElement && videoElement.videoWidth > 0 && dotNetRef) {
        const canvas = document.createElement('canvas');
        canvas.width = videoElement.videoWidth;
        canvas.height = videoElement.videoHeight;
        canvas.getContext('2d').drawImage(videoElement, 0, 0);
        const imageData = canvas.toDataURL('image/jpeg', 0.7);
        dotNetRef.invokeMethodAsync('DetectFrame', imageData);
    }
    
    animationId = requestAnimationFrame(detectLoop);
}

window.stopRealTimeDetection = () => {
    console.log("stopRealTimeDetection called");
    isDetecting = false;
    if (animationId) {
        cancelAnimationFrame(animationId);
        animationId = null;
    }
};

window.drawOnCanvas = (imageUrl) => {
    const canvas = document.getElementById('overlay');
    if (!canvas) return;
    
    const img = new Image();
    img.onload = () => {
        canvas.width = img.width;
        canvas.height = img.height;
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(img, 0, 0);
    };
    img.src = imageUrl;
};

window.clearCanvas = () => {
    const canvas = document.getElementById('overlay');
    if (canvas) {
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);
    }
};

window.capturePhoto = () => {
    if (!videoElement) return null;
    const canvas = document.createElement('canvas');
    canvas.width = videoElement.videoWidth;
    canvas.height = videoElement.videoHeight;
    canvas.getContext('2d').drawImage(videoElement, 0, 0);
    return canvas.toDataURL('image/jpeg', 0.9);
};

window.stopCamera = () => {
    console.log("stopCamera called");
    isDetecting = false;
    if (animationId) {
        cancelAnimationFrame(animationId);
        animationId = null;
    }
    if (stream) {
        stream.getTracks().forEach(track => track.stop());
        stream = null;
    }
    if (videoElement) {
        videoElement.srcObject = null;
    }
};