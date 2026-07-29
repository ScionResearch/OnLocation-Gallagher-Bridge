#!/usr/bin/env python3
"""Local CORS proxy for the Gallagher Command Centre API.

Run with:
    python proxy.py

Then open http://localhost:8081 in your browser. The proxy serves the
index.html app and forwards /api/* requests to the Gallagher server at
https://192.168.1.97:8904 (or the GALLAGHER_URL environment variable).
"""
import os
import ssl
import sys
from http.server import HTTPServer, SimpleHTTPRequestHandler
from urllib.request import Request, urlopen

# Backend Gallagher API base URL (no trailing slash)
GALLAGHER_URL = os.environ.get('GALLAGHER_URL', 'https://192.168.1.97:8904')
LISTEN_HOST = os.environ.get('PROXY_HOST', 'localhost')
LISTEN_PORT = int(os.environ.get('PROXY_PORT', '8081'))

# Trust the custom dev root CA if present, otherwise ignore backend cert errors.
ROOT_CA = os.path.join(os.path.dirname(__file__), 'certs', 'gallagher-dev-root.pem')
if os.path.exists(ROOT_CA):
    ssl_context = ssl.create_default_context(cafile=ROOT_CA)
else:
    ssl_context = ssl._create_unverified_context()

CORS_HEADERS = {
    'Access-Control-Allow-Origin': '*',
    'Access-Control-Allow-Methods': 'GET, POST, PUT, DELETE, OPTIONS, PATCH',
    'Access-Control-Allow-Headers': 'Authorization, Content-Type, Accept, X-Requested-With',
    'Access-Control-Allow-Credentials': 'true',
}


def add_cors_headers(handler):
    for key, value in CORS_HEADERS.items():
        handler.send_header(key, value)


class ProxyHandler(SimpleHTTPRequestHandler):
    # Serve files from the same folder the script lives in.
    directory = os.path.dirname(os.path.abspath(__file__))

    def do_OPTIONS(self):
        self.send_response(204, 'No Content')
        add_cors_headers(self)
        self.end_headers()

    def _proxy(self):
        path = self.path
        if not path.startswith('/api/'):
            # Not an API call; serve static files via SimpleHTTPRequestHandler.
            return SimpleHTTPRequestHandler.do_GET(self)

        target = GALLAGHER_URL + path
        method = self.command
        body = None
        content_length = self.headers.get('Content-Length')
        if content_length:
            body = self.rfile.read(int(content_length))

        # Forward relevant headers.
        forward_headers = {}
        for header in ('Authorization', 'Content-Type', 'Accept'):
            value = self.headers.get(header)
            if value:
                forward_headers[header] = value

        try:
            req = Request(target, data=body, headers=forward_headers, method=method)
            with urlopen(req, context=ssl_context, timeout=30) as resp:
                status = resp.status
                response_body = resp.read()
                content_type = resp.headers.get('Content-Type', 'application/octet-stream')
        except Exception as exc:
            message = f'Proxy error: {exc}'.encode('utf-8')
            self.send_response(502, 'Bad Gateway')
            add_cors_headers(self)
            self.send_header('Content-Type', 'text/plain; charset=utf-8')
            self.send_header('Content-Length', str(len(message)))
            self.end_headers()
            self.wfile.write(message)
            return

        self.send_response(status)
        add_cors_headers(self)
        self.send_header('Content-Type', content_type)
        self.send_header('Content-Length', str(len(response_body)))
        self.end_headers()
        self.wfile.write(response_body)

    def do_GET(self):
        if self.path.startswith('/api/'):
            self._proxy()
        else:
            if self.path in ('', '/'):
                self.path = '/index.html'
            try:
                SimpleHTTPRequestHandler.do_GET(self)
            except Exception as exc:
                self.send_error(500, f'Internal server error: {exc}')

    def do_POST(self):
        self._proxy()

    def do_PUT(self):
        self._proxy()

    def do_DELETE(self):
        self._proxy()

    def do_PATCH(self):
        self._proxy()

    def log_message(self, format, *args):
        # Suppress noisy default logging; print only API calls.
        if '/api/' in args[0]:
            super().log_message(format, *args)


if __name__ == '__main__':
    # Ensure a PEM copy of the root CA exists if the .cer is available.
    cer_path = os.path.join(os.path.dirname(__file__), 'certs', 'gallagher-dev-root.cer')
    pem_path = os.path.join(os.path.dirname(__file__), 'certs', 'gallagher-dev-root.pem')
    if os.path.exists(cer_path) and not os.path.exists(pem_path):
        try:
            import subprocess
            subprocess.run(
                ['openssl', 'x509', '-in', cer_path, '-inform', 'DER', '-out', pem_path],
                check=True,
                capture_output=True,
            )
        except Exception:
            pass

    server = HTTPServer((LISTEN_HOST, LISTEN_PORT), ProxyHandler)
    print(f'Proxy listening on http://{LISTEN_HOST}:{LISTEN_PORT}')
    print(f'Forwarding /api/* to {GALLAGHER_URL}/api/*')
    print('Open http://{}:{} in your browser'.format(LISTEN_HOST, LISTEN_PORT))
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print('\nShutting down.')
        server.shutdown()
        sys.exit(0)
