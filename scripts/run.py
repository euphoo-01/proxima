#!/usr/bin/env python3
import os
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
environment = os.environ.copy()
settings = root / '.env.oracle'
if settings.exists() and not environment.get('PROXIMA_ORACLE_CONNECTION'):
    values = {}
    for line in settings.read_text(encoding='utf-8').splitlines():
        if line.strip() and not line.lstrip().startswith('#') and '=' in line:
            key, value = line.split('=', 1)
            values[key.strip()] = value.strip()
    password = values.get('ORACLE_APP_PASSWORD', '')
    port = values.get('ORACLE_PORT', '1521')
    if password:
        if not port.isdigit() or any(c in password for c in ';"\r\n'):
            sys.exit('Проверьте формат локальной конфигурации Oracle.')
        environment['PROXIMA_ORACLE_CONNECTION'] = (
            f'User Id=PROXIMA_APP;Password={password};Data Source=localhost:{port}/FREEPDB1')

sys.exit(subprocess.call(['dotnet', 'run', '--project', 'src/Proxima.App/Proxima.App.csproj', *sys.argv[1:]],
                         cwd=root, env=environment))
