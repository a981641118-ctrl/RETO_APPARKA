window.appStorage = {
    set: (key, value) => localStorage.setItem(key, value),
    get: key => localStorage.getItem(key),
    remove: key => localStorage.removeItem(key)
};

window.appQr = {
    scanOnce: async function (videoId) {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            throw new Error("Camera API unavailable");
        }

        if (!("BarcodeDetector" in window)) {
            throw new Error("BarcodeDetector unavailable");
        }

        const video = document.getElementById(videoId);
        if (!video) {
            throw new Error("Video element not found");
        }

        const stream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: { ideal: "environment" } },
            audio: false
        });

        video.srcObject = stream;
        await video.play();

        const detector = new BarcodeDetector({ formats: ["qr_code"] });
        const timeoutAt = Date.now() + 15000;

        try {
            while (Date.now() < timeoutAt) {
                const results = await detector.detect(video);
                if (results.length > 0) {
                    return results[0].rawValue || "";
                }
                await new Promise(resolve => setTimeout(resolve, 350));
            }
            return "";
        } finally {
            stream.getTracks().forEach(track => track.stop());
            video.srcObject = null;
        }
    }
};
