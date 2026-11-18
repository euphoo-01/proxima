import argparse
from contextlib import ExitStack, redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location('ralph_runner', Path(__file__).with_name('ralph.py'))
ralph = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ralph)


def task(task_id='S01', blocked_by=None):
    return {'id': task_id, 'title': 'Срез', 'description': 'Проверяемое поведение',
            'acceptance': ['Сценарий работает'], 'stories': ['UC01'],
            'blocked_by': blocked_by or [], 'checks': [['check', task_id]]}


def result(status='completed'):
    return {'status': status, 'summary': 'Результат', 'evidence': ['Проверка'], 'next_steps': []}


class BacklogTests(unittest.TestCase):
    def load(self, tasks):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'backlog.json'
            path.write_text(json.dumps({'tasks': tasks}), encoding='utf-8')
            return ralph.load_tasks(path)

    def test_load_tasks_accepts_dependencies_independent_of_file_order(self):
        tasks = [task('S02', ['S01']), task()]
        self.assertEqual(self.load(tasks), tasks)

    def test_load_tasks_rejects_dependency_cycle(self):
        with self.assertRaisesRegex(ValueError, 'Цикл'):
            self.load([task('S01', ['S02']), task('S02', ['S01'])])

    def test_load_tasks_rejects_unknown_dependency(self):
        with self.assertRaisesRegex(ValueError, 'неизвестная зависимость'):
            self.load([task('S01', ['S99'])])

    def test_load_tasks_rejects_duplicate_ids(self):
        with self.assertRaisesRegex(ValueError, 'повторяются'):
            self.load([task(), task()])

    def test_load_tasks_rejects_shell_string_instead_of_argv(self):
        value = task()
        value['checks'] = ['check && something-else']
        with self.assertRaisesRegex(ValueError, 'argv'):
            self.load([value])

    def test_select_task_skips_blocked_and_completed_tasks(self):
        tasks = [task('S02', ['S01']), task()]
        self.assertEqual(ralph.select_task(tasks, {'tasks': {}})['id'], 'S01')
        state = {'tasks': {'S01': result()}}
        self.assertEqual(ralph.select_task(tasks, state)['id'], 'S02')

    def test_select_task_retries_before_unlocking_dependency(self):
        tasks = [task(), task('S02', ['S01'])]
        self.assertEqual(ralph.select_task(tasks, {'tasks': {'S01': result('retry')}})['id'], 'S01')
        self.assertIsNone(ralph.select_task(tasks, {'tasks': {'S01': result(), 'S02': result()}}))

    def test_validate_result_rejects_completion_without_evidence(self):
        value = result()
        value['evidence'] = []
        with self.assertRaisesRegex(ValueError, 'свидетельств'):
            ralph.validate_result(value)

    def test_validate_result_rejects_unknown_status_and_extra_fields(self):
        for value in [dict(result(), status='done'), dict(result(), approved=True)]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                ralph.validate_result(value)


class RunLoopTests(unittest.TestCase):
    def setUp(self):
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        temp = Path(self.stack.enter_context(tempfile.TemporaryDirectory()))
        self.stack.enter_context(patch.object(ralph, 'ROOT', temp))
        self.stack.enter_context(patch.object(ralph, 'STATE', temp))
        self.stack.enter_context(patch.object(ralph, 'digest', return_value='approved'))
        self.stack.enter_context(patch.object(ralph, 'tree_digest', return_value='tree'))
        self.stack.enter_context(patch.object(ralph.shutil, 'which', return_value='/bin/codex'))
        self.stack.enter_context(patch.object(ralph.subprocess, 'check_output',
                                            side_effect=lambda argv, **kwargs: '' if 'status' in argv else 'head\n'))
        self.stack.enter_context(patch.object(ralph, 'commit_iteration', return_value='head'))
        self.stack.enter_context(redirect_stdout(io.StringIO()))
        self.state = {'approved_digest': 'approved', 'started': True, 'head': 'head',
                      'tree_digest': 'tree', 'tasks': {}}
        self.args = argparse.Namespace(max_iterations=1, timeout=10, push=False)

    def test_run_loop_needs_user_stops_without_checks_or_next_task(self):
        with patch.object(ralph, 'ask_agent', return_value=result('needs_user')) as ask, \
                patch.object(ralph, 'execute') as execute:
            code = ralph.run_loop([task(), task('S02')], self.state, self.args)
        self.assertEqual(code, 2)
        self.assertEqual(ask.call_count, 1)
        execute.assert_not_called()
        self.assertEqual(self.state['tasks']['S01']['status'], 'needs_user')
        self.assertNotIn('S02', self.state['tasks'])
        self.assertEqual(ralph.read_json(ralph.STATE / 'state.json')['tasks']['S01']['status'], 'needs_user')

    def test_run_loop_failed_check_prevents_review_and_completion(self):
        with patch.object(ralph, 'ask_agent', return_value=result()) as ask, \
                patch.object(ralph, 'execute', return_value=1) as execute:
            code = ralph.run_loop([task()], self.state, self.args)
        self.assertEqual(code, 3)
        self.assertEqual(ask.call_count, 1)
        self.assertEqual(execute.call_args.args[0], ['check', 'S01'])
        self.assertEqual(self.state['tasks']['S01']['status'], 'retry')

    def test_run_loop_reviewer_rejection_prevents_completion(self):
        with patch.object(ralph, 'ask_agent', side_effect=[result(), result('retry')]) as ask, \
                patch.object(ralph, 'execute', return_value=0):
            code = ralph.run_loop([task()], self.state, self.args)
        self.assertEqual(code, 3)
        self.assertEqual([call.args[2] for call in ask.call_args_list], ['worker', 'reviewer'])
        self.assertEqual(self.state['tasks']['S01']['status'], 'retry')
        self.assertEqual(ask.call_args.args[3]['checks'][0]['exit_code'], 0)

    def test_run_loop_reviewed_completion_unlocks_dependent_task(self):
        self.args.max_iterations = 2
        with patch.object(ralph, 'ask_agent', side_effect=[result(), result(), result(), result()]) as ask, \
                patch.object(ralph, 'execute', return_value=0) as execute:
            code = ralph.run_loop([task(), task('S02', ['S01'])], self.state, self.args)
        self.assertEqual(code, 0)
        self.assertEqual([call.args[0]['id'] for call in ask.call_args_list], ['S01', 'S01', 'S02', 'S02'])
        self.assertEqual(execute.call_count, 2)
        self.assertEqual({key: value['status'] for key, value in self.state['tasks'].items()},
                         {'S01': 'completed', 'S02': 'completed'})

    def test_run_loop_requires_unblock_before_retrying_needs_user(self):
        self.state['tasks']['S01'] = result('needs_user')
        with patch.object(ralph, 'ask_agent') as ask, self.assertRaisesRegex(ValueError, 'ждёт пользователя'):
            ralph.run_loop([task()], self.state, self.args)
        ask.assert_not_called()

    def test_run_loop_refuses_unapproved_plan_before_agent_launch(self):
        self.state['approved_digest'] = 'old'
        with patch.object(ralph, 'ask_agent') as ask, self.assertRaisesRegex(ValueError, 'не одобрен'):
            ralph.run_loop([task()], self.state, self.args)
        ask.assert_not_called()

    def test_run_loop_stops_if_worker_changes_protected_files(self):
        with patch.object(ralph, 'digest', side_effect=['approved', 'changed', 'changed']), \
                patch.object(ralph, 'ask_agent', return_value=result()), \
                patch.object(ralph, 'execute') as execute, \
                self.assertRaisesRegex(ValueError, 'защищённые'):
            ralph.run_loop([task()], self.state, self.args)
        execute.assert_not_called()
        self.assertEqual(self.state['tasks']['S01']['status'], 'needs_user')
        saved = ralph.read_json(ralph.STATE / 'state.json')
        self.assertIn('защищённые', saved['tasks']['S01']['summary'])

    def test_run_loop_refuses_external_tree_change_before_agent_launch(self):
        with patch.object(ralph, 'tree_digest', return_value='external-change'), \
                patch.object(ralph, 'ask_agent') as ask, \
                self.assertRaisesRegex(ValueError, 'вне цикла'):
            ralph.run_loop([task()], self.state, self.args)
        ask.assert_not_called()
        self.assertEqual(self.state['tasks'], {})

    def test_run_loop_reviewer_timeout_persists_needs_user(self):
        error = subprocess.TimeoutExpired(['codex', 'exec'], 10)
        with patch.object(ralph, 'ask_agent', side_effect=[result(), error]) as ask, \
                patch.object(ralph, 'execute', return_value=0), \
                self.assertRaises(subprocess.TimeoutExpired):
            ralph.run_loop([task()], self.state, self.args)
        self.assertEqual([call.args[2] for call in ask.call_args_list], ['worker', 'reviewer'])
        saved = ralph.read_json(ralph.STATE / 'state.json')
        self.assertEqual(saved['tasks']['S01']['status'], 'needs_user')
        self.assertIn('timed out', saved['tasks']['S01']['summary'])
        self.assertEqual(saved['tree_digest'], 'tree')

    def test_run_loop_worker_invalid_json_persists_needs_user(self):
        error = json.JSONDecodeError('bad agent result', '{', 1)
        with patch.object(ralph, 'ask_agent', side_effect=error), \
                patch.object(ralph, 'execute') as execute, self.assertRaises(json.JSONDecodeError):
            ralph.run_loop([task()], self.state, self.args)
        execute.assert_not_called()
        saved = ralph.read_json(ralph.STATE / 'state.json')
        self.assertEqual(saved['tasks']['S01']['status'], 'needs_user')
        self.assertIn('bad agent result', saved['tasks']['S01']['summary'])
        self.assertTrue(Path(saved['tasks']['S01']['run_dir']).is_dir())

    def test_approve_rejects_backlog_that_is_still_draft(self):
        with patch.object(ralph, 'load_tasks', return_value=[task()]), \
                patch.object(ralph, 'read_json', return_value={'approved': False}), \
                patch.object(ralph, 'save_json') as save, \
                patch('sys.argv', ['ralph.py', 'approve']), \
                self.assertRaisesRegex(ValueError, 'approved: true'):
            ralph.main()
        save.assert_not_called()

    def test_run_loop_commit_failure_blocks_completed_task(self):
        with patch.object(ralph, 'ask_agent', side_effect=[result(), result()]), \
                patch.object(ralph, 'execute', return_value=0), \
                patch.object(ralph, 'commit_iteration', side_effect=RuntimeError('commit failed')):
            code = ralph.run_loop([task()], self.state, self.args)
        self.assertEqual(code, 2)
        self.assertEqual(self.state['tasks']['S01']['status'], 'needs_user')
        self.assertEqual(self.state['tasks']['S01']['attempted_status'], 'completed')

    def test_run_loop_retry_changes_are_checkpointed(self):
        with patch.object(ralph, 'ask_agent', return_value=result('retry')), \
                patch.object(ralph, 'commit_iteration', return_value='new-head') as commit:
            code = ralph.run_loop([task()], self.state, self.args)
        self.assertEqual(code, 3)
        self.assertEqual(commit.call_args.args[1]['status'], 'retry')
        self.assertEqual(self.state['tasks']['S01']['commit'], 'new-head')

    def test_run_loop_failure_changes_are_checkpointed(self):
        with patch.object(ralph, 'ask_agent', side_effect=RuntimeError('worker failed')), \
                patch.object(ralph, 'commit_iteration', return_value='new-head') as commit, \
                self.assertRaisesRegex(RuntimeError, 'worker failed'):
            ralph.run_loop([task()], self.state, self.args)
        self.assertEqual(commit.call_args.args[1]['status'], 'needs_user')
        self.assertEqual(self.state['tasks']['S01']['commit'], 'new-head')

    def test_run_loop_push_failure_preserves_local_commit_and_blocks_task(self):
        self.args.push = True
        with patch.object(ralph, 'ask_agent', side_effect=[result(), result()]), \
                patch.object(ralph, 'execute', return_value=0), \
                patch.object(ralph, 'commit_iteration', return_value='local-commit'), \
                patch.object(ralph, 'push_iteration', side_effect=RuntimeError('push failed')):
            code = ralph.run_loop([task()], self.state, self.args)
        self.assertEqual(code, 2)
        self.assertEqual(self.state['tasks']['S01']['commit'], 'local-commit')
        self.assertEqual(self.state['tasks']['S01']['status'], 'needs_user')
        self.assertNotIn('pushed', self.state['tasks']['S01'])

    def test_run_loop_persists_new_commit_head_for_resume(self):
        with patch.object(ralph, 'ask_agent', return_value=result('retry')), \
                patch.object(ralph, 'commit_iteration', return_value='new-head'), \
                patch.object(ralph, 'current_head', side_effect=['head', 'new-head']):
            ralph.run_loop([task()], self.state, self.args)
        saved = ralph.read_json(ralph.STATE / 'state.json')
        self.assertEqual(saved['head'], 'new-head')
        self.assertEqual(saved['tasks']['S01']['iteration_start_head'], 'head')

    def test_run_loop_sends_compact_history_without_past_evidence(self):
        self.state['tasks']['S00'] = dict(result(), summary='x' * 800, evidence=['secret-large-history'])
        self.state['tasks']['S01'] = dict(result('retry'), next_steps=['fix'] * 20)
        with patch.object(ralph, 'ask_agent', return_value=result('needs_user')) as ask:
            ralph.run_loop([task()], self.state, self.args)
        feedback = ask.call_args.args[3]
        self.assertEqual(len(feedback['completed_tasks']['S00']['summary']), 500)
        self.assertNotIn('evidence', feedback['completed_tasks']['S00'])
        self.assertNotIn('evidence', feedback['previous_attempt'])
        self.assertEqual(len(feedback['previous_attempt']['next_steps']), 8)


class IterationCommitTests(unittest.TestCase):
    def setUp(self):
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        self.temp = Path(self.stack.enter_context(tempfile.TemporaryDirectory()))
        self.stack.enter_context(patch.object(ralph, 'ROOT', self.temp))

    def test_commit_iteration_labels_completed_and_wip_distinctly(self):
        for status, suffix in [('completed', 'complete'), ('retry', 'wip retry'), ('needs_user', 'wip needs_user')]:
            with self.subTest(status=status), \
                    patch.object(ralph, 'current_head', side_effect=['old', 'new']), \
                    patch.object(ralph.subprocess, 'check_output', side_effect=[' M file\n', '']), \
                    patch.object(ralph, 'execute', return_value=0) as execute:
                head = ralph.commit_iteration(task(), result(status), 'old', self.temp, 10)
            self.assertEqual(head, 'new')
            self.assertEqual(execute.call_args_list[0].args[0], ['git', 'add', '--all', '--', '.'])
            self.assertEqual(execute.call_args.args[0], ['git', 'commit', '-m', f'chore(ralph): S01 {suffix}'])

    def test_commit_iteration_skips_empty_tree(self):
        with patch.object(ralph, 'current_head', return_value='old'), \
                patch.object(ralph.subprocess, 'check_output', return_value=''), \
                patch.object(ralph, 'execute') as execute:
            self.assertEqual(ralph.commit_iteration(task(), result(), 'old', self.temp, 10), 'old')
        execute.assert_not_called()

    def test_commit_iteration_refuses_agent_created_commit(self):
        with patch.object(ralph, 'current_head', return_value='external'), \
                patch.object(ralph, 'execute') as execute, self.assertRaisesRegex(ValueError, 'HEAD изменён'):
            ralph.commit_iteration(task(), result(), 'old', self.temp, 10)
        execute.assert_not_called()

    def test_commit_iteration_git_add_failure_prevents_commit(self):
        with patch.object(ralph, 'current_head', return_value='old'), \
                patch.object(ralph.subprocess, 'check_output', return_value=' M file\n'), \
                patch.object(ralph, 'execute', return_value=1) as execute, \
                self.assertRaisesRegex(RuntimeError, 'подготовить'):
            ralph.commit_iteration(task(), result(), 'old', self.temp, 10)
        self.assertEqual(execute.call_count, 1)

    def test_commit_iteration_git_commit_failure_is_not_success(self):
        with patch.object(ralph, 'current_head', return_value='old'), \
                patch.object(ralph.subprocess, 'check_output', return_value=' M file\n'), \
                patch.object(ralph, 'execute', side_effect=[0, 1]), \
                self.assertRaisesRegex(RuntimeError, 'Коммит не создан'):
            ralph.commit_iteration(task(), result(), 'old', self.temp, 10)

    def test_push_iteration_targets_origin_current_branch_without_force(self):
        with patch.object(ralph.subprocess, 'check_output', side_effect=['work/ralph\n', 'https://example.com/repo']), \
                patch.object(ralph, 'execute', return_value=0) as execute:
            ralph.push_iteration(self.temp, 10)
        self.assertEqual(execute.call_args.args[0], ['git', 'push', 'origin', 'HEAD:refs/heads/work/ralph'])


if __name__ == '__main__':
    unittest.main()
