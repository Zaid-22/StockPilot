"""CI-only HTTPS smoke test against a loopback app and temporary certificate."""
import http.cookiejar
import json
import os
import ssl
import urllib.error
import urllib.request

BASE = 'https://127.0.0.1:5443'
cookies = http.cookiejar.CookieJar()
# Only the fixed loopback endpoint uses this temporary self-signed certificate.
context = ssl._create_unverified_context()
browser = urllib.request.build_opener(
    urllib.request.HTTPSHandler(context=context),
    urllib.request.HTTPCookieProcessor(cookies),
)


def request(path, payload=None, token=None, method='GET'):
    headers = {'Accept': 'application/json'}
    data = None
    if payload is not None:
        data = json.dumps(payload).encode()
        headers['Content-Type'] = 'application/json'
    if token:
        headers['X-CSRF-TOKEN'] = token
    req = urllib.request.Request(BASE + path, data=data, headers=headers, method=method)
    try:
        return browser.open(req, timeout=10)
    except urllib.error.HTTPError as error:
        return error


assert request('/api/health/ready').status == 200
assert b'<div id="root">' in request('/login').read()
assert request('/api/missing').status == 404
assert request('/api/auth/me').status == 401
token = json.load(request('/api/auth/csrf'))['token']
response = request('/api/auth/login', {
    'email': os.environ['Bootstrap__AdminEmail'],
    'password': os.environ['Bootstrap__AdminPassword'],
}, token, 'POST')
assert response.status == 200, response.read()
assert 'Admin' in json.load(response)['roles']
session_cookie = next(cookie for cookie in cookies if cookie.name == 'StockPilot.Session')
assert session_cookie.secure
assert request('/api/auth/me').status == 200
assert request('/api/admin/users').status == 200
token = json.load(request('/api/auth/csrf'))['token']
assert request('/api/auth/logout', token=token, method='POST').status == 204
assert request('/api/auth/me').status == 401
print('Published HTTPS app: static routes, health, secure login, admin access and logout passed.')
