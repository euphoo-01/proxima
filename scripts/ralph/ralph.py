#!/usr/bin/env python3
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
BACKLOG = ROOT / 'docs/implementation/backlog.json'
STATE = ROOT / '.ralph'


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8'))


def save_json(path, value):
    temp = path.with_suffix('.tmp')
    temp.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    temp.replace(path)


def load_tasks(path=BACKLOG):
    tasks = read_json(path)['tasks']
    ids = [t['id'] for t in tasks]
    if not tasks or len(ids) != len(set(ids)):
        raise ValueError('Задачи отсутствуют или повторяются ID')
    for task in tasks:
        if not re.fullmatch(r'S\d{2}', task['id']):
            raise ValueError('Некорректный ID задачи')
        for key in ('title', 'description'):
            if not isinstance(task.get(key), str) or not task[key].strip():
                raise ValueError(f'{task["id"]}: отсутствует {key}')
        for key in ('acceptance', 'stories', 'blocked_by', 'checks'):
            if not isinstance(task.get(key), list):
                raise ValueError(f'{task["id"]}: {key} должен быть списком')
        if not task['acceptance'] or not task['checks']:
            raise ValueError(f'{task["id"]}: нужны критерии и команды проверки')
        if any(d not in ids for d in task['blocked_by']):
            raise ValueError(f'{task["id"]}: неизвестная зависимость')
        for cmd in task['checks']:
            if not isinstance(cmd, list) or not cmd or any(not isinstance(a, str) or not a for a in cmd):
                raise ValueError('Команда проверки должна быть непустым argv')
    done = set()
    while len(done) < len(tasks):
        ready = [t['id'] for t in tasks if t['id'] not in done and set(t['blocked_by']) <= done]
        if not ready:
            raise ValueError('Цикл зависимостей')
        done.update(ready)
    return tasks


def digest():
    paths = [BACKLOG, ROOT / 'docs/oracle-requirements.md']
    paths += sorted(HERE.glob('*.md')) + sorted(HERE.glob('*.py'))
    paths += [HERE / 'result.schema.json'] + sorted((ROOT / 'docs/adr').glob('*.md'))
    value = hashlib.sha256()
    for path in paths:
        value.update(str(path.relative_to(ROOT)).encode())
        value.update(path.read_bytes())
    return value.hexdigest()


def tree_digest():
    names = subprocess.check_output(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=ROOT)
    value = hashlib.sha256()
    for name in sorted(set(names.split(b'\0')) - {b''}):
        path = ROOT / os.fsdecode(name)
        value.update(name + b'\0')
        if path.is_symlink():
            value.update(os.fsencode(os.readlink(path)))
        elif path.is_file():
            value.update(path.read_bytes())
        else:
            value.update(b'<deleted>')
    return value.hexdigest()


def select_task(tasks, state):
    done = {key for key, value in state.get('tasks', {}).items() if value['status'] == 'completed'}
    return next((t for t in tasks if t['id'] not in done and set(t['blocked_by']) <= done), None)


def execute(argv, log, timeout, prompt=None):
    with log.open('w', encoding='utf-8') as output:
        proc = subprocess.Popen(argv, cwd=ROOT, stdin=subprocess.PIPE if prompt is not None else subprocess.DEVNULL,
                                stdout=output, stderr=subprocess.STDOUT, text=True, start_new_session=True)
        try:
            proc.communicate(input=prompt, timeout=timeout)
        except BaseException:
            os.killpg(proc.pid, signal.SIGTERM)
            try:
                proc.wait(timeout=5)
            except subprocess.TimeoutExpired:
                os.killpg(proc.pid, signal.SIGKILL)
                proc.wait()
            raise
        return proc.returncode


def validate_result(result):
    if set(result) != {'status', 'summary', 'evidence', 'next_steps'}:
        raise ValueError('Некорректные поля ответа агента')
    if result['status'] not in ('completed', 'retry', 'needs_user') or not isinstance(result['summary'], str):
        raise ValueError('Некорректный статус ответа')
    for field in ('evidence', 'next_steps'):
        if not isinstance(result[field], list) or any(not isinstance(x, str) for x in result[field]):
            raise ValueError(f'Некорректный {field}')
    if result['status'] == 'completed' and not result['evidence']:
        raise ValueError('Нет свидетельств выполнения')
    return result


def ask_agent(task, run_dir, role, feedback, timeout):
    result_file = run_dir / f'{role}.json'
    prompt = (HERE / 'agent.md').read_text(encoding='utf-8')
    if role == 'reviewer':
        prompt += '\n' + (HERE / 'reviewer.md').read_text(encoding='utf-8')
    prompt += '\nЗадача:\n' + json.dumps(task, ensure_ascii=False)
    prompt += '\nПредыдущие результаты и проверки:\n' + json.dumps(feedback, ensure_ascii=False)
    argv = ['codex', 'exec', '--sandbox', 'read-only' if role == 'reviewer' else 'workspace-write',
            '-C', str(ROOT), '--color', 'never', '--output-schema', str(HERE / 'result.schema.json'),
            '-o', str(result_file), '-']
    if execute(argv, run_dir / f'{role}.log', timeout, prompt):
        raise RuntimeError(f'Codex завершился с ошибкой; см. {run_dir}')
    return validate_result(read_json(result_file))


def current_head():
    return subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()


def commit_iteration(task, result, start_head, run_dir, timeout):
    if current_head() != start_head:
        raise ValueError('HEAD изменён агентом или другим процессом; автоматический коммит отменён')
    dirty = subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True)
    if not dirty.strip():
        return start_head
    if execute(['git', 'add', '--all', '--', '.'], run_dir / 'git-add.log', timeout):
        raise RuntimeError('Не удалось подготовить коммит; проверьте git-add.log и индекс')
    outcome = 'complete' if result['status'] == 'completed' else f'wip {result["status"]}'
    message = f'chore(ralph): {task["id"]} {outcome}'
    if execute(['git', 'commit', '-m', message], run_dir / 'git-commit.log', timeout):
        raise RuntimeError('Коммит не создан; проверьте git-commit.log и индекс')
    head = current_head()
    if head == start_head:
        raise RuntimeError('Git сообщил успех, но HEAD не изменился')
    dirty = subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True)
    if dirty.strip():
        raise RuntimeError('После коммита остались изменения; проверьте hooks и рабочее дерево')
    return head


def push_iteration(run_dir, timeout):
    branch = subprocess.check_output(['git', 'symbolic-ref', '--short', 'HEAD'], cwd=ROOT, text=True).strip()
    subprocess.check_output(['git', 'remote', 'get-url', 'origin'], cwd=ROOT, text=True)
    target = f'HEAD:refs/heads/{branch}'
    if execute(['git', 'push', 'origin', target], run_dir / 'git-push.log', timeout):
        raise RuntimeError(f'Push не выполнен. Коммит сохранён локально; проверьте git-push.log. '
                           f'После устранения причины: git push origin {target}')


def finish_iteration(task, result, state, run_dir, start_head, args):
    result['iteration_start_head'] = start_head
    result['run_dir'] = str(run_dir)
    try:
        if digest() != state['approved_digest']:
            raise ValueError('Защищённые файлы изменены; автоматический коммит запрещён')
        result['commit'] = commit_iteration(task, result, start_head, run_dir, args.timeout)
        if digest() != state['approved_digest']:
            raise ValueError('Защищённые файлы изменены git hook; push запрещён')
        if args.push:
            push_iteration(run_dir, args.timeout)
            result['pushed'] = True
    except (ValueError, OSError, RuntimeError, subprocess.SubprocessError, KeyboardInterrupt) as error:
        result['attempted_status'] = result['status']
        result['status'] = 'needs_user'
        result['summary'] = f'{result["summary"]}; Git: {str(error) or "прервано"}'
        result['next_steps'].append('Проверить Git-логи, HEAD, индекс и diff; устранить причину и снять блокировку')
    state['tasks'][task['id']] = result
    state['head'] = current_head()
    state['tree_digest'] = tree_digest()
    save_json(STATE / 'state.json', state)
    return result


def run_loop(tasks, state, args):
    if state.get('approved_digest') != digest():
        raise ValueError('План не одобрен или изменился. Сначала согласуйте его и выполните approve.')
    if not shutil.which('codex'):
        raise ValueError('Codex CLI не найден')
    if not state.get('started'):
        dirty = subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True)
        if dirty.strip():
            raise ValueError('Первый запуск требует чистого Git-дерева. Сохраните изменения самостоятельно.')
    elif state.get('head') != subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip():
        raise ValueError('HEAD изменился с начала цикла; проверьте состояние перед продолжением')
    elif state.get('tree_digest') != tree_digest():
        raise ValueError('Рабочее дерево изменилось вне цикла. Проверьте diff и выполните checkpoint.')
    state['started'] = True
    state['head'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    state['tree_digest'] = tree_digest()
    save_json(STATE / 'state.json', state)
    for _ in range(args.max_iterations):
        task = select_task(tasks, state)
        if task is None:
            print('Все задачи завершены.')
            return 0
        old = state['tasks'].get(task['id'], {})
        if old.get('status') == 'needs_user':
            raise ValueError(f'{task["id"]} ждёт пользователя; после решения выполните unblock {task["id"]}')
        dirty = subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True)
        if dirty.strip():
            raise ValueError('Перед итерацией требуется чистое дерево: сохраните внешние изменения отдельно')
        start_head = current_head()
        run_dir = STATE / f'{time.time_ns()}-{task["id"]}'
        run_dir.mkdir()
        print(f'{task["id"]}: {task["title"]}; логи: {run_dir}', flush=True)
        previous = {key: old[key] for key in ('status', 'summary', 'run_dir') if key in old}
        previous['next_steps'] = old.get('next_steps', [])[:8]
        completed = {key: {'summary': value.get('summary', '')[:500]}
                     for key, value in state['tasks'].items() if value['status'] == 'completed'}
        feedback = {'previous_attempt': previous, 'completed_tasks': completed, 'iteration_start_head': start_head}
        try:
            result = ask_agent(task, run_dir, 'worker', feedback, args.timeout)
            if digest() != state['approved_digest']:
                raise ValueError('Агент изменил защищённые файлы плана/цикла; остановлено без отката')
            if result['status'] == 'completed':
                checks = []
                for index, argv in enumerate(task['checks']):
                    log = run_dir / f'check-{index}.log'
                    code = execute(argv, log, args.timeout)
                    checks.append({'argv': argv, 'exit_code': code, 'log': str(log)})
                    if code:
                        result = {'status': 'retry', 'summary': 'Обязательная команда проверки завершилась с ошибкой',
                                  'evidence': [str(log)], 'next_steps': ['Прочитать лог и исправить причину; не ослаблять проверки']}
                        break
                if result['status'] == 'completed':
                    result = ask_agent(task, run_dir, 'reviewer',
                                       {'worker': result, 'checks': checks, 'iteration_start_head': start_head}, args.timeout)
            if digest() != state['approved_digest']:
                raise ValueError('Защищённые файлы изменились во время проверки; остановлено без отката')
        except (ValueError, OSError, RuntimeError, subprocess.SubprocessError, KeyboardInterrupt) as error:
            result = {'status': 'needs_user', 'summary': str(error) or 'Прервано пользователем',
                      'evidence': [str(run_dir)], 'next_steps': ['Проверить логи и оставшиеся изменения']}
            finish_iteration(task, result, state, run_dir, start_head, args)
            raise
        result = finish_iteration(task, result, state, run_dir, start_head, args)
        print(f'{result["status"]}: {result["summary"]}', flush=True)
        if result['status'] == 'needs_user':
            return 2
    if select_task(tasks, state) is None:
        print('Все задачи завершены.')
        return 0
    print('Лимит итераций достигнут. Прогресс сохранён; следующий run продолжит работу.')
    return 3


def main():
    parser = argparse.ArgumentParser(description='Ограниченный Ralph loop для вертикальных срезов Proxima')
    parser.add_argument('command', choices=['plan', 'approve', 'run', 'status', 'unblock', 'checkpoint'])
    parser.add_argument('task', nargs='?')
    parser.add_argument('--max-iterations', type=int, default=1)
    parser.add_argument('--timeout', type=int, default=1800)
    parser.add_argument('--push', action='store_true', help='после каждой итерации push в origin текущей ветки')
    args = parser.parse_args()
    if args.max_iterations < 1 or args.timeout < 1:
        parser.error('Лимиты должны быть положительными')
    tasks = load_tasks()
    if args.command == 'plan':
        for task in tasks:
            print(f'{task["id"]}. {task["title"]}\n  зависит от: {", ".join(task["blocked_by"]) or "—"}'
                  f'\n  сценарии: {", ".join(task["stories"])}')
        return 0
    STATE.mkdir(exist_ok=True)
    with (STATE / 'lock').open('a') as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise ValueError('Другой цикл уже работает в этом репозитории')
        state_path = STATE / 'state.json'
        state = read_json(state_path) if state_path.exists() else {'tasks': {}}
        if args.command == 'approve':
            if read_json(BACKLOG).get('approved') is not True:
                raise ValueError('Сначала согласуйте задачи и установите approved: true в backlog.json')
            if state.get('approved_digest') not in (None, digest()) and state['tasks']:
                raise ValueError('План изменился: завершение задач нужно пересмотреть. Сохраните старую .ralph и начните новое состояние.')
            state['approved_digest'] = digest()
            save_json(state_path, state)
            print('Текущий план одобрен локально. Запуск: python3 scripts/ralph/ralph.py run --max-iterations 1')
        elif args.command == 'checkpoint':
            if not state.get('started'):
                raise ValueError('Цикл ещё не запущен')
            state['tree_digest'] = tree_digest()
            state['head'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
            save_json(state_path, state)
            print('Осмотренные изменения приняты как текущая точка продолжения.')
        elif args.command == 'status':
            for task in tasks:
                result = state['tasks'].get(task['id'], {})
                print(f'{task["id"]}: {result.get("status", "pending")} {result.get("summary", "")}')
        elif args.command == 'unblock':
            if args.task not in {t['id'] for t in tasks}:
                raise ValueError('Укажите существующий ID задачи')
            result = state['tasks'].get(args.task, {})
            if result.get('status') != 'needs_user':
                raise ValueError('Задача не ожидает пользователя')
            result['status'] = 'retry'
            save_json(state_path, state)
            print('Блокировка снята. Опишите принятое решение в документации перед продолжением.')
        else:
            return run_loop(tasks, state, args)
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, KeyError, OSError, RuntimeError, subprocess.SubprocessError) as error:
        print(f'Остановлено: {error}', file=sys.stderr)
        sys.exit(2)
    except KeyboardInterrupt:
        print('Прервано пользователем.', file=sys.stderr)
        sys.exit(130)
