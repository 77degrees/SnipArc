@echo off
REM Double-click this to read the docs.
REM
REM index.html loads the .md files at runtime so it can never show stale copies,
REM but browsers block reading local files from file:// - so it needs a server.
REM This serves the REPO ROOT (not docs/) because the changelog lives one level up,
REM then opens the right page. Port 0 lets the OS pick a free one, so nothing
REM collides with a dev server you already have running.
cd /d "%~dp0.."

set "SERVE=import http.server,socketserver,webbrowser,threading;h=http.server.SimpleHTTPRequestHandler;s=socketserver.TCPServer(('127.0.0.1',0),h);p=s.server_address[1];threading.Timer(0.5,lambda:webbrowser.open(f'http://127.0.0.1:{p}/docs/')).start();print(f'Docs at http://127.0.0.1:{p}/docs/');print('Close this window to stop.');s.serve_forever()"

REM Try each launcher in turn; contributors will not all have the same one.
py -3 -c "%SERVE%" 2>nul && goto :eof
py -c "%SERVE%" 2>nul && goto :eof
python -c "%SERVE%" 2>nul && goto :eof
python3 -c "%SERVE%" 2>nul && goto :eof

echo.
echo Could not find Python, which this launcher uses to serve the docs.
echo.
echo Either install it from https://python.org and run this again, or read the
echo markdown directly - docs\README.md is the index and every file is plain text.
echo.
pause
