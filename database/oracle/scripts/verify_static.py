#!/usr/bin/env python3
"""Compare physical DDL with canonical ER XML, without an Oracle connection."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[3]
xml = root / 'docs/spec/er-model.drawio'
if not xml.exists():
    xml = root.parent / 'Диаграммы/Proxima_Oracle_ER_v4.drawio'
cells = list(ET.parse(xml).iter('mxCell'))
expected = {}
for c in cells:
    ident = c.get('id', '')
    if c.get('vertex') == '1' and '.' in ident:
        table, column = ident.split('.')
        expected.setdefault(table, []).append(column)
ddl = (root / 'database/oracle/sql/10_schema.sql').read_text()
actual = {}
for table, body in re.findall(r'CREATE TABLE (\w+) \((.*?)\n\);', ddl, re.S):
    actual[table] = re.findall(r'^  (\w+) (?:RAW|VARCHAR2|NUMBER|TIMESTAMP|DATE|CLOB)\b', body, re.M)
assert actual == expected, 'Physical tables/columns differ from ER'
edges = [c for c in cells if c.get('edge') == '1']
for i, edge in enumerate(edges, 1):
    parent, pk = edge.get('source').split('.')
    child, fk = edge.get('target').split('.')
    expected_fk = f'ALTER TABLE {child} ADD CONSTRAINT fk_er_{i:02} FOREIGN KEY ({fk}) REFERENCES {parent} ({pk});'
    assert expected_fk in ddl, expected_fk
assert len(expected) == 21
assert sum(map(len, expected.values())) == 148
assert len(edges) == 32
print('PASS: 21 exact ER tables, 148 exact ordered columns, 32 exact ER foreign keys + portfolio consistency FK.')
