const songs = [
  { title: "It's All About Me", artist: "Mýa · 1998", key: 60, color: "#ff78bf" },
  { title: "The Best of Me", artist: "Mýa · 2000", key: 62, color: "#a990ff" },
  { title: "Case of the Ex", artist: "Mýa · 2000", key: 64, color: "#ffca78" },
  { title: "Lady Marmalade", artist: "Mýa · 2001", key: 57, color: "#72ecd7" },
  { title: "My Love Is Like...Wo", artist: "Mýa · 2003", key: 65, color: "#fd85a8" },
  { title: "Fallen", artist: "Mýa · 2003", key: 59, color: "#97a8ff" },
];
const noteNames = ["C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B"];
const phrase = [0, 4, 7, 5, 3, 2, 4, 0, 7, 5, 4, 2];
const el = (id) => document.getElementById(id);
const els = {
  setlist: el("setlist"), start: el("start-button"), stop: el("stop-button"), timer: el("timer"),
  target: el("target-note"), guidance: el("live-guidance"), pitch: el("pitch-note"), status: el("pitch-status"),
  fill: el("pitch-fill"), level: el("level-bar"), levelValue: el("level-value"), canvas: el("waveform"),
  empty: el("wave-empty"), micNote: el("mic-note"), lastGrade: el("last-grade"), lastScore: el("last-score"),
  lastSong: el("last-song"), install: el("install-button"),
};
let selectedSong = 0;
let media = null;
let audioContext = null;
let analyser = null;
let samples = null;
let raf = 0;
let startedAt = 0;
let history = [];
let pitchFrames = [];
let deferredInstall = null;
let lastTargetStep = -1;
const ctx = els.canvas.getContext("2d");

function renderSetlist() {
  els.setlist.innerHTML = songs.map((song, index) => `
    <button class="song-option" type="button" data-song="${index}" aria-pressed="${index === selectedSong}">
      <span class="song-number">${String(index + 1).padStart(2, "0")}</span>
      <span class="song-info"><b>${song.title}</b><small>${song.artist}</small></span>
      <span class="song-key">${noteNames[song.key % 12]} warm-up</span>
    </button>`).join("");
}

function noteName(midi) {
  return `${noteNames[((midi % 12) + 12) % 12]}${Math.floor(midi / 12) - 1}`;
}

function setTarget() {
  const step = Math.floor((performance.now() / 1000 - startedAt) / 2.5);
  const index = step % phrase.length;
  const midi = songs[selectedSong].key + phrase[index];
  els.target.textContent = noteName(midi);
  els.guidance.textContent = step < 0 ? "A new note every few seconds" : `Note ${index + 1} of ${phrase.length} · hold it steady`;
  if (step !== lastTargetStep) {
    lastTargetStep = step;
    els.target.animate([{ transform: "scale(.86)", opacity: .3 }, { transform: "scale(1)", opacity: 1 }], { duration: 360, easing: "ease-out" });
  }
  return midi;
}

function pitchFrom(samplesIn, sampleRate) {
  let mean = 0;
  for (let i = 0; i < samplesIn.length; i++) mean += samplesIn[i];
  mean /= samplesIn.length;
  let rms = 0;
  for (let i = 0; i < samplesIn.length; i++) {
    samplesIn[i] -= mean;
    rms += samplesIn[i] * samplesIn[i];
  }
  rms = Math.sqrt(rms / samplesIn.length);
  if (rms < 0.012) return { rms, midi: null };
  const stride = 4;
  const length = Math.floor(samplesIn.length / stride);
  const signal = new Float32Array(length);
  for (let i = 0; i < length; i++) signal[i] = samplesIn[i * stride];
  const rate = sampleRate / stride;
  const minLag = Math.floor(rate / 1000);
  const maxLag = Math.min(Math.floor(rate / 65), length - 1);
  let bestLag = -1;
  let best = 0;
  for (let lag = minLag; lag <= maxLag; lag++) {
    let correlation = 0;
    for (let i = 0; i < length - lag; i++) correlation += signal[i] * signal[i + lag];
    correlation /= length - lag;
    if (correlation > best) { best = correlation; bestLag = lag; }
  }
  const frequency = bestLag > 0 ? rate / bestLag : 0;
  return { rms, midi: best > 0.56 && frequency > 0 ? Math.round(69 + 12 * Math.log2(frequency / 440)) : null };
}

function paintWaveform() {
  if (!analyser || !samples) return;
  analyser.getFloatTimeDomainData(samples);
  const rect = els.canvas.getBoundingClientRect();
  const scale = window.devicePixelRatio || 1;
  if (els.canvas.width !== Math.round(rect.width * scale) || els.canvas.height !== Math.round(rect.height * scale)) {
    els.canvas.width = Math.round(rect.width * scale);
    els.canvas.height = Math.round(rect.height * scale);
  }
  ctx.clearRect(0, 0, els.canvas.width, els.canvas.height);
  const height = els.canvas.height, width = els.canvas.width;
  ctx.beginPath();
  for (let i = 0; i < samples.length; i += 4) {
    const x = (i / samples.length) * width;
    const y = (0.5 + samples[i] * 2.6) * height;
    if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
  }
  ctx.strokeStyle = "#ff86cd";
  ctx.lineWidth = Math.max(1.5, scale * 1.4);
  ctx.shadowColor = "#f773c9";
  ctx.shadowBlur = 12 * scale;
  ctx.stroke();
  ctx.shadowBlur = 0;
}

function tick() {
  if (!analyser || !media) return;
  paintWaveform();
  const result = pitchFrom(samples, audioContext.sampleRate);
  const targetMidi = setTarget();
  const level = Math.min(1, result.rms * 6);
  els.level.style.width = `${(level * 100).toFixed(1)}%`;
  els.levelValue.textContent = `${Math.round(level * 100)}%`;
  els.empty.hidden = level > 0.015;
  if (result.midi !== null) {
    const delta = result.midi - targetMidi;
    const accuracy = Math.max(0, 100 - Math.abs(delta) * 22);
    pitchFrames.push({ accuracy, midi: result.midi, target: targetMidi });
    els.pitch.textContent = noteName(result.midi);
    els.status.textContent = Math.abs(delta) <= 1 ? "On pitch — keep going!" : `${Math.abs(delta)} ${Math.abs(delta) === 1 ? "semitone" : "semitones"} ${delta > 0 ? "high" : "low"}`;
    els.status.style.color = accuracy >= 78 ? "var(--teal)" : "var(--gold)";
    const visualOffset = Math.max(-1, Math.min(1, delta / 8));
    els.fill.style.width = `${Math.abs(visualOffset) * 46}%`;
    els.fill.style.left = `${50 + visualOffset * 46}%`;
  } else {
    els.pitch.textContent = "—";
    els.status.textContent = level > .04 ? "Try a steady vowel" : "Sing into your mic";
    els.status.style.color = "";
    els.fill.style.width = "0";
  }
  const elapsed = (performance.now() / 1000) - startedAt;
  const remaining = Math.max(0, 60 - elapsed);
  els.timer.textContent = `${String(Math.floor(remaining / 60)).padStart(2, "0")}:${String(Math.floor(remaining % 60)).padStart(2, "0")}`;
  if (remaining <= 0) { finishSession(); return; }
  raf = requestAnimationFrame(tick);
}

async function startSession() {
  if (media) return;
  els.micNote.textContent = "Requesting microphone access…";
  els.start.disabled = true;
  try {
    media = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: false } });
    audioContext = new (window.AudioContext || window.webkitAudioContext)();
    await audioContext.resume();
    analyser = audioContext.createAnalyser();
    analyser.fftSize = 4096;
    analyser.smoothingTimeConstant = .25;
    audioContext.createMediaStreamSource(media).connect(analyser);
    samples = new Float32Array(analyser.fftSize);
    pitchFrames = [];
    startedAt = performance.now() / 1000;
    lastTargetStep = -1;
    els.timer.textContent = "01:00";
    els.start.hidden = true;
    els.stop.disabled = false;
    els.micNote.textContent = "Live pitch analysis · your microphone audio never leaves this device.";
    document.querySelector(".challenge-card").classList.add("is-running");
    els.empty.hidden = false;
    raf = requestAnimationFrame(tick);
  } catch (error) {
    if (media) media.getTracks().forEach((track) => track.stop());
    media = null;
    if (audioContext) await audioContext.close();
    audioContext = null;
    els.start.disabled = false;
    const message = error.name === "NotAllowedError" ? "Microphone access was blocked. Allow it in Chrome's address-bar settings, then try again." :
      error.name === "NotFoundError" ? "No microphone found. Connect one and try again." :
      "Microphone access needs Chrome on a secure connection (HTTPS).";
    els.micNote.textContent = message;
    els.micNote.classList.add("shake");
    setTimeout(() => els.micNote.classList.remove("shake"), 400);
  }
}

async function finishSession() {
  if (!media) return;
  cancelAnimationFrame(raf);
  media.getTracks().forEach((track) => track.stop());
  media = null;
  if (audioContext) await audioContext.close();
  audioContext = null;
  analyser = null;
  els.start.hidden = false;
  els.start.disabled = false;
  els.stop.disabled = true;
  document.querySelector(".challenge-card").classList.remove("is-running");
  els.timer.textContent = "01:00";
  els.level.style.width = "0";
  els.levelValue.textContent = "—";
  els.empty.hidden = false;
  els.micNote.textContent = "Your mic is off. Start another take whenever you like.";
  if (!pitchFrames.length) {
    els.micNote.textContent = "No clear notes detected this take. Try a steady vowel, move closer to your mic, and sing again.";
    return;
  }
  const average = Math.round(pitchFrames.reduce((sum, frame) => sum + frame.accuracy, 0) / pitchFrames.length);
  const grade = average >= 92 ? "S" : average >= 82 ? "A" : average >= 68 ? "B" : average >= 52 ? "C" : "D";
  els.lastScore.textContent = average;
  els.lastGrade.textContent = grade;
  els.lastSong.textContent = `${songs[selectedSong].title} · ${pitchFrames.length} notes heard`;
  history.unshift({ score: average, grade, title: songs[selectedSong].title, at: Date.now() });
  history = history.slice(0, 10);
  try { localStorage.setItem("mya-music-challenge-scores", JSON.stringify(history)); } catch { /* storage can be disabled */ }
  els.micNote.textContent = `${grade === "S" ? "Stunning take!" : "Nice work!"} Your pitch-match score is saved on this device.`;
  document.querySelector(".score-card").animate([{ boxShadow: "0 0 0 #ff88cc00" }, { boxShadow: "0 0 28px #ff88cc55" }, { boxShadow: "0 0 0 #ff88cc00" }], { duration: 1000 });
}

els.setlist.addEventListener("click", (event) => {
  const button = event.target.closest("[data-song]");
  if (!button || media) return;
  selectedSong = Number(button.dataset.song);
  renderSetlist();
  setTarget();
});
els.start.addEventListener("click", startSession);
els.stop.addEventListener("click", finishSession);
els.setlist.setAttribute("aria-label", "Choose a song-inspired vocal warm-up");
el("song-count").textContent = `${String(songs.length).padStart(2, "0")} TRACKS`;
renderSetlist();
try {
  history = JSON.parse(localStorage.getItem("mya-music-challenge-scores") || "[]");
  if (history[0]) {
    els.lastScore.textContent = history[0].score;
    els.lastGrade.textContent = history[0].grade;
    els.lastSong.textContent = `${history[0].title} · previous take`;
  }
} catch { history = []; }

window.addEventListener("beforeinstallprompt", (event) => {
  event.preventDefault();
  deferredInstall = event;
  els.install.hidden = false;
});
els.install.addEventListener("click", async () => {
  if (!deferredInstall) return;
  deferredInstall.prompt();
  await deferredInstall.userChoice;
  deferredInstall = null;
  els.install.hidden = true;
});
window.addEventListener("appinstalled", () => { els.install.hidden = true; });
if ("serviceWorker" in navigator && location.protocol.startsWith("http")) {
  window.addEventListener("load", () => navigator.serviceWorker.register("./service-worker.js").catch(() => {}));
}
