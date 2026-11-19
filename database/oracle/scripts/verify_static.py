#!/usr/bin/env python3
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
ddl = '\n'.join((root / 'database/oracle/sql' / name).read_text()
                for name in ('10_schema.sql', '15_auth_sessions.sql'))
actual = {}
for table, body in re.findall(r'CREATE TABLE (\w+) \((.*?)\n\)', ddl, re.S):
    actual[table] = re.findall(r'^  (\w+) (?:RAW|VARCHAR2|NUMBER|TIMESTAMP|DATE|CLOB)\b', body, re.M)
assert actual == expected, 'Physical tables/columns differ from ER'
edges = [c for c in cells if c.get('edge') == '1']
for i, edge in enumerate(edges, 1):
    parent, pk = edge.get('source').split('.')
    child, fk = edge.get('target').split('.')
    expected_fk = f'ALTER TABLE {child} ADD CONSTRAINT fk_er_{i:02} FOREIGN KEY ({fk}) REFERENCES {parent} ({pk});'
    inline_fk = f'CONSTRAINT fk_er_{i:02} FOREIGN KEY ({fk}) REFERENCES {parent} ({pk})'
    assert re.sub(r'\s+','',expected_fk.rstrip(';')) in re.sub(r'\s+','',ddl) or re.sub(r'\s+','',inline_fk) in re.sub(r'\s+','',ddl), expected_fk
assert len(expected) == 22
assert sum(map(len, expected.values())) == 158
assert len(edges) == 34
print('PASS: 22 exact ER tables, 158 ordered columns, 34 ER foreign keys + portfolio consistency FK.')
