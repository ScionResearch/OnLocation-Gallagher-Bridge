#!/usr/bin/env python3
"""Download the OnLocation OpenAPI spec from the Gatsby page-data JSON.

The API docs site stores the full OpenAPI definition in each tag page's
page-data.json under result.data.contentItem.data.redocStoreStr.
"""
import gzip
import json
import os
import ssl
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, 'ref')
OUT_PATH = os.path.join(OUT_DIR, 'whosonlocation-openapi.json')
PAGE_DATA_URL = 'https://apidocs.whosonlocation.com/page-data/openapi/wol/tag/Assets/page-data.json'


def fetch(url):
    ctx = ssl.create_default_context()
    req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
    with urllib.request.urlopen(req, context=ctx, timeout=60) as resp:
        raw = resp.read()
        if resp.headers.get('Content-Encoding') == 'gzip':
            raw = gzip.decompress(raw)
        return raw.decode('utf-8')


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    print('Fetching page-data.json...')
    page_data_text = fetch(PAGE_DATA_URL)
    page_data = json.loads(page_data_text)

    # The OpenAPI spec is stored as a JSON string inside redocStoreStr
    redoc_store_str = page_data['result']['data']['contentItem']['data']['redocStoreStr']
    redoc_store = json.loads(redoc_store_str)

    # The spec is under definition.data
    openapi = redoc_store['definition']['data']

    # Add a note about where this came from
    openapi['_meta'] = {
        'source_url': PAGE_DATA_URL,
        'extracted_from': 'apidocs.whosonlocation.com Gatsby page-data',
    }

    with open(OUT_PATH, 'w', encoding='utf-8') as f:
        json.dump(openapi, f, indent=2, ensure_ascii=False)

    info = openapi.get('info', {})
    paths = list(openapi.get('paths', {}).keys())
    schemas = list(openapi.get('components', {}).get('schemas', {}).keys())
    tags = [t.get('name') for t in openapi.get('tags', [])]
    print(f"Wrote {OUT_PATH}")
    print(f"  title: {info.get('title')}")
    print(f"  version: {info.get('version')}")
    print(f"  paths: {len(paths)}")
    print(f"  schemas: {len(schemas)}")
    print(f"  tags: {len(tags)}")
    print(f"  tags: {', '.join(tags[:5])}...")


if __name__ == '__main__':
    main()
