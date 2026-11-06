
let mediaSource = null;
let sourceBuffer = null;
let audioElement = null;
let bufferQueue = [];
let isBufferAppending = false;
let maxBufferDuration = 30;
let base64Total = "";
//audioUrl = $"data:audio/mp3;base64,{base64Audio}";
export function init(audioElementId, data) {
	console.log(`init audio element ${audioElementId}`);
	audioElement = document.getElementById(audioElementId);
	audioElement.src = data;
	//mediaSource = new MediaSource();
	//audioElement.src = URL.createObjectURL(mediaSource);

	//mediaSource.addEventListener("sourceopen", () => {
	//	sourceBuffer = mediaSource.addSourceBuffer("audio/mpeg");
	//	sourceBuffer.addEventListener("updateend", () => {
	//		isBufferAppending = false;

	//		if (bufferQueue.length > 0) {
	//			appendBufferFromQueue();
	//		}
	//		console.log(`audio element ${audioElementId} src = ${audioElement.src}`);
	//	});
	//});
	//audioElement.addEventListener("timeupdate", () => {
	//	removeOldBuffer();
	//});
}

export function appendBuffer(base64String) {
	base64Total += base64String;
	const binaryString = window.atob(base64String);
	const len = binaryString.length;
	const bytes = new Uint8Array(len);
	for (let i = 0; i < len; i++) {
		bytes[i] = binaryString.charCodeAt(i);
	}
	bufferQueue.push(bytes.buffer);

	if (!isBufferAppending) {
		appendBufferFromQueue();
	}
}

export function appendBufferFromQueue() {
	if (bufferQueue.length > 0 && !sourceBuffer.updating) {
		isBufferAppending = true;
		try {
			sourceBuffer.appendBuffer(bufferQueue.shift());
		} catch (e) {
			console.warn("SourceBuffer error");
			console.log(JSON.stringify(sourceBuffer, null, 2));
			setSourceFromFullData();
		}
		
	}
}
function setSourceFromFullData() {
	if (audioElement) {
		audioElement.src = `data:audio/mpeg;base64,${base64Total}`;
	}
}
export function removeOldBuffer() {
	const currentTime = audioElement.currentTime;
	if (currentTime > maxBufferDuration && !sourceBuffer.updating) {
		const removeTime = currentTime - maxBufferDuration;
		sourceBuffer.remove(0, removeTime);
	}
}

export function endOfStream() {
	if (mediaSource && mediaSource.readyState === "open" && sourceBuffer && !sourceBuffer.updating) {
		mediaSource.endOfStream();
		return true;
	}
	return false;
}

export function play(elementId) {
	audioElement = document.getElementById(elementId);
	if (audioElement) {
		audioElement.play();
	}
}

export function pause(elementId) {
	audioElement = document.getElementById(elementId);
	if (audioElement) {
		audioElement.pause();
	}
}

export function changeProgress(value, elementId) {
	audioElement = document.getElementById(elementId);
	if (audioElement) {
		audioElement.currentTime = (value / 100) * audioElement.duration;
	}
}

export function getProgress(elementId) {
	audioElement = document.getElementById(elementId);
	if (audioElement) {
		return (audioElement.currentTime / audioElement.duration) * 100;
	}
	return 0.0;
}

export function getCurrentTime(elementId) {
	audioElement = document.getElementById(elementId);
	if (audioElement) {
		const currentTime = audioElement.currentTime;
		console.log(`current time: ${currentTime}`);
		return currentTime;
	}
	return 0;
}

export function getDuration(elementId) {
	audioElement = document.getElementById(elementId);
	if (audioElement) {
		const duration = audioElement.duration;
		console.log(`duration: ${duration}`);
		return duration;
	}
	return 0;
}