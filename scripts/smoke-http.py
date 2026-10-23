"""CI-only HTTPS smoke test against a loopback app and temporary certificate."""
import http.cookiejar
import json
import os
import ssl
import urllib.error
import urllib.request
import uuid

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


def mutate(path, payload, method='POST', expected=200):
    csrf = json.load(request('/api/auth/csrf'))['token']
    response = request(path, payload, csrf, method)
    assert response.status == expected, (response.status, response.read())
    return json.load(response)


# A unique prefix makes the smoke workflow safe to repeat on its disposable DB.
prefix = uuid.uuid4().hex[:12]
category = mutate('/api/catalog/categories', {'name': f'Smoke {prefix}'}, expected=201)
product_input = {
    'name': 'Smoke keyboard', 'sku': f' test-{prefix} ', 'categoryId': category['id'],
    'sellingPrice': 25.125, 'reorderLevel': 3, 'isActive': True,
}
product = mutate('/api/catalog/products', product_input, expected=201)
assert product['sku'] == f'TEST-{prefix.upper()}'
assert product['quantityOnHand'] == 0
path = '/api/catalog/products/' + product['id']
assert json.load(request(path))['sellingPrice'] == 25.125
page = json.load(request('/api/catalog/products?q=' + prefix + '&page=1&pageSize=1'))
assert page['totalCount'] == 1 and page['items'][0]['id'] == product['id']
mutate('/api/catalog/products', product_input, expected=409)
update = {**product_input, 'name': 'Updated keyboard', 'version': product['version']}
updated = mutate(path, update, 'PUT')
mutate(path, update, 'PUT', expected=409)
deactivated = mutate(path, {**update, 'version': updated['version'], 'isActive': False}, 'PUT')
assert not deactivated['isActive'] and deactivated['quantityOnHand'] == 0
assert json.load(request(path))['name'] == 'Updated keyboard'
for kind in ['suppliers', 'customers']:
    contact = mutate('/api/catalog/' + kind, {
        'name': f'Smoke {prefix}', 'email': 'smoke@example.test', 'phone': '12345',
    }, expected=201)
    saved = mutate(f'/api/catalog/{kind}/{contact["id"]}', {
        **contact, 'address': 'Amman', 'isActive': False,
    }, 'PUT')
    assert not saved['isActive'] and saved['address'] == 'Amman'
token = json.load(request('/api/auth/csrf'))['token']
assert request('/api/auth/logout', token=token, method='POST').status == 204
assert request('/api/auth/me').status == 401
print('Published HTTPS app: routes, health, secure login, catalog persistence/conflicts/deactivation and logout passed.')
