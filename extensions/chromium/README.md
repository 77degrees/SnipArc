# SnipArc Browser Capture

This Manifest V3 extension captures the visible tab, opens a private local editor, and supports crop, clipboard copy, and PNG download. Captures are kept in extension session storage and are never uploaded.

## Load for development

1. Open `edge://extensions` or `chrome://extensions`.
2. Enable **Developer mode**.
3. Choose **Load unpacked**.
4. Select this `extensions/chromium` directory.
5. Pin SnipArc and click its toolbar icon on a normal web page.

Browser-protected pages such as `edge://`, `chrome://`, extension stores, and some PDF viewers cannot be captured by extensions. Store publication requires developer accounts and final store IDs; those are release operations, not source-code changes.
