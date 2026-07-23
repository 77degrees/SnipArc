"use strict";

chrome.action.onClicked.addListener(async (tab) => {
  try {
    const dataUrl = await chrome.tabs.captureVisibleTab(tab.windowId, {
      format: "png"
    });
    await chrome.storage.session.set({
      pendingCapture: {
        dataUrl,
        capturedAt: new Date().toISOString(),
        sourceTitle: tab.title || "Browser tab"
      }
    });
    await chrome.tabs.create({ url: chrome.runtime.getURL("editor.html") });
  } catch (error) {
    console.error("SnipArc could not capture the visible tab.", error);
  }
});
