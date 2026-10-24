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
    if method == 'POST' and path.startswith('/api/catalog/'):
        payload = {'requestId': str(uuid.uuid4()), **payload}
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
    contact_input = {
        'requestId': str(uuid.uuid4()),
        'name': f'Smoke {prefix}', 'email': 'smoke@example.test', 'phone': '12345',
    }
    contact = mutate('/api/catalog/' + kind, contact_input, expected=201)
    assert mutate('/api/catalog/' + kind, contact_input, expected=201) == contact
    saved = mutate(f'/api/catalog/{kind}/{contact["id"]}', {
        **contact, 'address': 'Amman', 'isActive': False,
    }, 'PUT')
    assert not saved['isActive'] and saved['address'] == 'Amman'
# Exercise all business phases against the published application.
product = mutate(path, {**update, 'version': deactivated['version'], 'isActive': True}, 'PUT')
supplier = mutate('/api/catalog/suppliers', {'name': f'Delivery supplier {prefix}'}, expected=201)
customer = mutate('/api/catalog/customers', {'name': f'Delivery customer {prefix}'}, expected=201)
balance = json.load(request('/api/inventory/balances?productId=' + product['id']))['items'][0]
adjustment = {'requestId': str(uuid.uuid4()), 'productId': product['id'], 'version': balance['version'],
              'quantityChange': 2, 'reason': 'Opening count'}
first = mutate('/api/inventory/adjustments', adjustment)
assert mutate('/api/inventory/adjustments', adjustment) == first


def create_order(kind, party_id, quantity, price):
    order = mutate('/api/orders/' + kind, {'requestId': str(uuid.uuid4()), 'partyId': party_id,
        'lines': [{'productId': product['id'], 'quantity': quantity, 'unitPrice': price}]}, expected=201)
    return mutate(f'/api/orders/{kind}/{order["id"]}/confirm',
                  {'requestId': str(uuid.uuid4()), 'version': order['version']})


purchase = create_order('purchases', supplier['id'], 5, 4.125)
receipt = {'requestId': str(uuid.uuid4()), 'version': purchase['version'], 'reference': 'Smoke delivery',
           'lines': [{'lineId': purchase['lines'][0]['id'], 'quantity': 3}]}
partial = mutate(f'/api/orders/purchases/{purchase["id"]}/receive', receipt)
assert partial['status'] == 'Partial'
assert mutate(f'/api/orders/purchases/{purchase["id"]}/receive', receipt) == partial
sale = create_order('sales', customer['id'], 2, 12.125)
shipment = {'requestId': str(uuid.uuid4()), 'version': sale['version'], 'reference': 'Smoke shipment',
            'lines': [{'lineId': sale['lines'][0]['id'], 'quantity': 2}]}
completed = mutate(f'/api/orders/sales/{sale["id"]}/ship', shipment)
assert completed['status'] == 'Completed'
assert mutate(f'/api/orders/sales/{sale["id"]}/ship', shipment) == completed
balance = json.load(request('/api/inventory/balances?productId=' + product['id']))['items'][0]
assert balance['quantityOnHand'] == 3
mutate('/api/inventory/adjustments', {**adjustment, 'requestId': str(uuid.uuid4()),
       'version': balance['version'], 'quantityChange': -4}, expected=409)
journal = json.load(request('/api/inventory/movements?productId=' + product['id']))
assert journal['totalCount'] == 3
assert sum(row['quantityChange'] for row in journal['items']) == 3
assert request('/api/reports/summary').status == 200
for route in ['/inventory', '/purchases', '/sales', '/reports']:
    assert b'<div id="root">' in request(route).read()

token = json.load(request('/api/auth/csrf'))['token']
assert request('/api/auth/logout', token=token, method='POST').status == 204
assert request('/api/auth/me').status == 401
print('Published HTTPS app: authentication, catalog, inventory, purchasing, sales, reports, retries and logout passed.')
