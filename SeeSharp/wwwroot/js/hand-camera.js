console.log("hand-camera.js loaded");

let handVideoElement = null;
let handStream = null;
let handDotNetRef = null;
let handAnimationId = null;
let handIsDetecting = false;
let hands = null;
let lastTimestamp = 0;
let frameCount = 0;
let fps = 0;

const handLandmarks = {
    wrist: 0,
    thumb: { cmc: 1, mcp: 2, ip: 3, tip: 4 },
    index: { mcp: 5, pip: 6, dip: 7, tip: 8 },
    middle: { mcp: 9, pip: 10, dip: 11, tip: 12 },
    ring: { mcp: 13, pip: 14, dip: 15, tip: 16 },
    pinky: { mcp: 17, pip: 18, dip: 19, tip: 20 }
};

const connections = [
    [0, 1], [1, 2], [2, 3], [3, 4],
    [0, 5], [5, 6], [6, 7], [7, 8],
    [0, 9], [9, 10], [10, 11], [11, 12],
    [0, 13], [13, 14], [14, 15], [15, 16],
    [0, 17], [17, 18], [18, 19], [19, 20],
    [5, 9], [9, 13], [13, 17]
];

function getRaisedFingers(landmarks) {
    let raised = 0;
    if (landmarks[handLandmarks.index.tip].y < landmarks[handLandmarks.index.mcp].y) raised++;
    if (landmarks[handLandmarks.middle.tip].y < landmarks[handLandmarks.middle.mcp].y) raised++;
    if (landmarks[handLandmarks.ring.tip].y < landmarks[handLandmarks.ring.mcp].y) raised++;
    if (landmarks[handLandmarks.pinky.tip].y < landmarks[handLandmarks.pinky.mcp].y) raised++;

    const thumbTip = landmarks[handLandmarks.thumb.tip];
    const thumbMCP = landmarks[handLandmarks.thumb.mcp];
    if (Math.abs(thumbTip.x - thumbMCP.x) > 0.05) raised++;

    return raised;
}

function getGesture(raisedFingers) {
    if (raisedFingers === 0) return "Кулак";
    if (raisedFingers === 1) return "Указательный";
    if (raisedFingers === 2) return "Мир";
    if (raisedFingers === 3) return "Окей";
    if (raisedFingers === 4) return "Коза";
    if (raisedFingers === 5) return "Ладонь";
    return "Жест";
}

async function initMediaPipe() {
    try {
        if (typeof Hands === 'undefined') {
            console.error("Hands not loaded");
            return false;
        }

        hands = new Hands({
            locateFile: (file) => {
                return `/${file}`;
            }
        });

        hands.setOptions({
            maxNumHands: 2,
            modelComplexity: 1,
            minDetectionConfidence: 0.5,
            minTrackingConfidence: 0.5,
            selfieMode: true
        });

        hands.onResults(onHandResults);

        console.log("MediaPipe ready");
        return true;
    } catch (err) {
        console.error(err);
        return false;
    }
}

function onHandResults(results) {
    if (!handIsDetecting) return;

    const now = performance.now();
    frameCount++;
    if (now - lastTimestamp >= 1000) {
        fps = frameCount;
        frameCount = 0;
        lastTimestamp = now;
    }

    const canvas = document.getElementById('handOverlay');
    if (!canvas || !results.image) return;

    canvas.width = results.image.width;
    canvas.height = results.image.height;
    const ctx = canvas.getContext('2d');

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(results.image, 0, 0);

    let handsCount = 0;
    let totalRaised = 0;
    let mainGesture = "Нет рук";

    if (results.multiHandLandmarks && results.multiHandLandmarks.length > 0) {
        handsCount = results.multiHandLandmarks.length;

        for (let handIdx = 0; handIdx < results.multiHandLandmarks.length; handIdx++) {
            const landmarks = results.multiHandLandmarks[handIdx];

            const pixelLandmarks = landmarks.map(lm => ({
                x: lm.x * canvas.width,
                y: lm.y * canvas.height
            }));

            ctx.beginPath();
            ctx.strokeStyle = '#00FF00';
            ctx.lineWidth = 2;
            for (const conn of connections) {
                const start = pixelLandmarks[conn[0]];
                const end = pixelLandmarks[conn[1]];
                if (start && end) {
                    ctx.beginPath();
                    ctx.moveTo(start.x, start.y);
                    ctx.lineTo(end.x, end.y);
                    ctx.stroke();
                }
            }

            for (let i = 0; i < pixelLandmarks.length; i++) {
                const lm = pixelLandmarks[i];
                ctx.beginPath();
                ctx.arc(lm.x, lm.y, 4, 0, 2 * Math.PI);
                ctx.fillStyle = '#00FF00';
                ctx.fill();
            }

            const raised = getRaisedFingers(landmarks);
            totalRaised += raised;
            if (handIdx === 0) mainGesture = getGesture(raised);
        }
    }

    if (handDotNetRef) {
        handDotNetRef.invokeMethodAsync('OnHandDetected', handsCount, totalRaised, mainGesture, fps);
    }
}

window.startHandCamera = async () => {
    handVideoElement = document.getElementById('handVideo');
    if (!handVideoElement) return false;

    try {
        await initMediaPipe();

        handStream = await navigator.mediaDevices.getUserMedia({
            video: { width: 640, height: 480 }
        });
        handVideoElement.srcObject = handStream;
        await handVideoElement.play();

        return true;
    } catch (err) {
        console.error(err);
        return false;
    }
};

window.startHandRealTimeDetection = () => {
    if (handAnimationId) cancelAnimationFrame(handAnimationId);
    handIsDetecting = true;

    function detect() {
        if (!handIsDetecting) return;
        if (handVideoElement && handVideoElement.readyState >= 2 && hands) {
            hands.send({ image: handVideoElement });
        }
        handAnimationId = requestAnimationFrame(detect);
    }
    detect();
};

window.stopHandRealTimeDetection = () => {
    handIsDetecting = false;
    if (handAnimationId) {
        cancelAnimationFrame(handAnimationId);
        handAnimationId = null;
    }
};

window.clearHandCanvas = () => {
    const canvas = document.getElementById('handOverlay');
    if (canvas) {
        canvas.getContext('2d').clearRect(0, 0, canvas.width, canvas.height);
    }
};

window.setHandDotNetReference = (reference) => {
    handDotNetRef = reference;
};

window.stopHandCamera = () => {
    handIsDetecting = false;
    if (handAnimationId) {
        cancelAnimationFrame(handAnimationId);
        handAnimationId = null;
    }
    if (handStream) {
        handStream.getTracks().forEach(track => track.stop());
        handStream = null;
    }
    if (hands) {
        hands.close();
        hands = null;
    }
};