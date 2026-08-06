# Self-hosted translation backend

SnipSnap's **Translation endpoint** setting speaks the LibreTranslate API. It POSTs
`{q, source, target, format}` and reads `{translatedText}` back. Any service with that
contract works; LibreTranslate is the reference implementation.

Translation is off until you fill the endpoint in. Screenshots never leave the machine —
only the text OCR already extracted is sent, and only when you press Translate.

## Run it

```bash
mkdir -p models && chown -R 1032:65534 models
docker compose up -d
```

The `chown` is required, not optional. The image runs as uid 1032 / gid 65534, but Docker
creates a missing bind-mount directory as root, so the container cannot write its model cache.

The resulting failure is misleading. `argostranslate` raises `PermissionError` on
`~/.local/share/argos-translate/packages` while the entrypoint is still assembling arguments,
so the argument string comes out empty and the visible error is:

```
gunicorn: error: argument -w/--workers: expected one argument
```

The container then restart-loops. If you see that, check directory ownership rather than
the gunicorn invocation.

First boot downloads eight language models (`en, es, fr, de, pt, zh, ja, ko` — the set the
"Translate to" dropdown offers). Expect several minutes and roughly 2 GB. The `./models`
volume keeps them across restarts; without it every restart re-downloads.

Confirm it is up:

```bash
curl http://localhost:5000/languages
```

## Point SnipSnap at it

SnipSnap requires **HTTPS**, and accepts plain HTTP only for a service on the same computer
(`SettingsWindow.xaml.cs`, the endpoint validation). A LAN address over HTTP is rejected.
That leaves two working options.

### Loopback over an SSH tunnel

Simplest, and the traffic is encrypted by SSH:

```bash
ssh -N -L 5000:localhost:5000 root@<docker-host>
```

Endpoint: `http://127.0.0.1:5000/translate`

The tunnel must be running for Translate to work. Without it the request fails with a
connection error.

### TLS in front

Put the container behind a reverse proxy holding a certificate your machine already
trusts, then use `https://translate.example.com/translate`.

A self-signed certificate will **not** work — .NET's `HttpClient` rejects untrusted chains,
so the request fails before it reaches the service. Either use a real certificate or install
the self-signed one into the machine's Trusted Root store.

## API key

The compose file leaves key auth off, so the key box in Settings stays empty.

Hosted services are different. The official `libretranslate.com` is a paid service: it requires
a key and answers 403 without one. SnipSnap reports that as "the endpoint refused the request
because it needs an API key." Paste the key into **Translation API key** and it is sent as
`api_key` on each request.

The software itself is free and AGPL-licensed (<https://github.com/LibreTranslate/LibreTranslate>);
only the hosted instance costs money. Running the container above gets you the same API with no
key and no per-request fee, which is why it is the recommended setup.

The key is stored in plain text in the SnipSnap settings JSON under `%LOCALAPPDATA%`. Treat it
as a low-value credential and prefer a self-hosted instance that needs no key at all.

## Path gotcha

The endpoint is the full path including `/translate`. A base URL alone returns 404, which
SnipSnap reports with that specific hint.
