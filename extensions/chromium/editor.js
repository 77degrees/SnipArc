"use strict";

const canvas = document.querySelector("#capture");
const context = canvas.getContext("2d", { alpha: false });
const status = document.querySelector("#status");
const meta = document.querySelector("#capture-meta");
const buttons = {
  crop: document.querySelector("#crop"),
  reset: document.querySelector("#reset"),
  copy: document.querySelector("#copy"),
  download: document.querySelector("#download")
};

let original = null;
let dragStart = null;
let selection = null;

function setEnabled(enabled) {
  buttons.reset.disabled = !enabled;
  buttons.copy.disabled = !enabled;
  buttons.download.disabled = !enabled;
  buttons.crop.disabled = !enabled || !selection;
}

function canvasPoint(event) {
  const rect = canvas.getBoundingClientRect();
  return {
    x: Math.max(0, Math.min(canvas.width, (event.clientX - rect.left) * canvas.width / rect.width)),
    y: Math.max(0, Math.min(canvas.height, (event.clientY - rect.top) * canvas.height / rect.height))
  };
}

function normalizedRect(start, end) {
  const left = Math.floor(Math.min(start.x, end.x));
  const top = Math.floor(Math.min(start.y, end.y));
  const right = Math.ceil(Math.max(start.x, end.x));
  const bottom = Math.ceil(Math.max(start.y, end.y));
  return {
    x: left,
    y: top,
    width: Math.max(1, right - left),
    height: Math.max(1, bottom - top)
  };
}

function redraw() {
  if (!original) return;
  context.drawImage(original, 0, 0, canvas.width, canvas.height);
  if (!selection) return;

  context.save();
  context.fillStyle = "rgb(0 0 0 / 52%)";
  context.fillRect(0, 0, canvas.width, selection.y);
  context.fillRect(0, selection.y, selection.x, selection.height);
  context.fillRect(selection.x + selection.width, selection.y,
    canvas.width - selection.x - selection.width, selection.height);
  context.fillRect(0, selection.y + selection.height,
    canvas.width, canvas.height - selection.y - selection.height);
  context.strokeStyle = "#4c8dff";
  context.lineWidth = Math.max(2, canvas.width / 900);
  context.strokeRect(selection.x, selection.y, selection.width, selection.height);
  context.restore();
}

async function loadCapture() {
  const { pendingCapture } = await chrome.storage.session.get("pendingCapture");
  if (!pendingCapture?.dataUrl) {
    status.textContent = "No pending capture was found. Click the SnipArc toolbar button and try again.";
    return;
  }

  const image = new Image();
  image.onload = () => {
    original = image;
    canvas.width = image.naturalWidth;
    canvas.height = image.naturalHeight;
    redraw();
    meta.textContent = `${pendingCapture.sourceTitle} • ${image.naturalWidth} × ${image.naturalHeight}`;
    status.textContent = "Ready. Nothing has been uploaded.";
    setEnabled(true);
  };
  image.onerror = () => {
    status.textContent = "The captured image could not be opened.";
  };
  image.src = pendingCapture.dataUrl;
}

canvas.addEventListener("pointerdown", (event) => {
  if (!original) return;
  canvas.setPointerCapture(event.pointerId);
  dragStart = canvasPoint(event);
  selection = null;
  setEnabled(true);
  redraw();
});

canvas.addEventListener("pointermove", (event) => {
  if (!dragStart || !canvas.hasPointerCapture(event.pointerId)) return;
  selection = normalizedRect(dragStart, canvasPoint(event));
  buttons.crop.disabled = false;
  redraw();
});

canvas.addEventListener("pointerup", (event) => {
  if (!dragStart) return;
  selection = normalizedRect(dragStart, canvasPoint(event));
  dragStart = null;
  canvas.releasePointerCapture(event.pointerId);
  buttons.crop.disabled = false;
  redraw();
});

buttons.crop.addEventListener("click", () => {
  if (!selection) return;
  const output = document.createElement("canvas");
  output.width = selection.width;
  output.height = selection.height;
  output.getContext("2d", { alpha: false }).drawImage(
    original,
    selection.x, selection.y, selection.width, selection.height,
    0, 0, selection.width, selection.height);
  const image = new Image();
  image.onload = () => {
    original = image;
    canvas.width = image.naturalWidth;
    canvas.height = image.naturalHeight;
    selection = null;
    redraw();
    setEnabled(true);
    status.textContent = `Cropped to ${canvas.width} × ${canvas.height}.`;
  };
  image.src = output.toDataURL("image/png");
});

buttons.reset.addEventListener("click", async () => {
  selection = null;
  await loadCapture();
});

buttons.copy.addEventListener("click", async () => {
  canvas.toBlob(async (blob) => {
    try {
      await navigator.clipboard.write([new ClipboardItem({ "image/png": blob })]);
      status.textContent = "Copied to the clipboard.";
    } catch (error) {
      status.textContent = `Copy failed: ${error.message}`;
    }
  }, "image/png");
});

buttons.download.addEventListener("click", () => {
  canvas.toBlob((blob) => {
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `SnipArc ${new Date().toISOString().replaceAll(":", "-").slice(0, 19)}.png`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    status.textContent = "Download started.";
  }, "image/png");
});

loadCapture().catch((error) => {
  status.textContent = `Capture could not be loaded: ${error.message}`;
});
