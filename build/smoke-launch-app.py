#!/usr/bin/env python3
"""Starts the packaged desktop app (DROPMORPH_SMOKE_APP, set by build/smoke-portable.ps1), checks that it stays up, that a
second start hands over to it (macOS) and that `dropmorph --quit` ends it cleanly. On Linux it runs under Xvfb in an X11
session and fails when the app logged that the X11 input hook back end could not be loaded.

Used by .github/workflows/portable.yml. Screenshots and the startup log go to $RUNNER_TEMP.
"""

import os
import pathlib
import signal
import subprocess
import sys
import time

app = os.environ['DROPMORPH_SMOKE_APP']
temp = pathlib.Path(os.environ.get('RUNNER_TEMP', '/tmp'))
shots = temp / 'dropmorph-screens'
shots.mkdir(exist_ok=True)
is_linux = sys.platform.startswith('linux')
is_macos = sys.platform == 'darwin'
command = [app, '--settings', '--install-engines=', '--context-menu=off', '--start-with-system=off']
if is_linux:
    command = ['xvfb-run', '-a', 'env', '-u', 'WAYLAND_DISPLAY', 'XDG_SESSION_TYPE=x11', *command]
log_path = temp / 'dropmorph-startup.log'


def dropmorph_logs():
    for folder in (pathlib.Path.home() / '.config/DropMorph/logs', pathlib.Path.home() / 'Library/Application Support/DropMorph/logs'):
        if folder.is_dir():
            yield from sorted(folder.glob('*.log'))


def check_input_hook():
    """The SharpHook loader reports ErrorLinuxLoadBackend when dlopen of libuiohook-xrecord.so fails."""
    lines = [line for path in dropmorph_logs() for line in path.read_text(errors='replace').splitlines()]
    for line in lines:
        if 'input hook' in line:
            print(line)
    if any('ErrorLinuxLoadBackend' in line for line in lines):
        raise RuntimeError('The X11 input hook back end could not be loaded (ErrorLinuxLoadBackend).')


with log_path.open('w') as log:
    process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    try:
        time.sleep(8)
        if process.poll() is not None:
            raise RuntimeError('The desktop app exited before the startup check.')
        if is_macos:
            # The real screen with DropMorph's window, as a person would see it (uploaded with the results).
            subprocess.run(['screencapture', '-x', str(shots / 'settings.png')], check=False, timeout=30)
            # A second start forwards to the running DropMorph, which brings its window to the front.
            try:
                subprocess.run([app], check=False, timeout=30)
            except subprocess.TimeoutExpired:
                raise RuntimeError('A second start did not hand over to the running DropMorph.')
            time.sleep(4)
            subprocess.run(['screencapture', '-x', str(shots / 'second-start.png')], check=False, timeout=30)
            if process.poll() is not None:
                raise RuntimeError('The desktop app exited after a second start.')
        quit_code = 'import os, sys; os.setsid(); os.execv(sys.argv[1], [sys.argv[1], "--quit"])'
        # Reap DropMorph as soon as it ends while --quit waits for it, as launchd or a shell would: a process
        # that has ended but was not reaped by its parent (this script) still counts as running.
        quitter = subprocess.Popen([sys.executable, '-c', quit_code, app])
        quit_deadline = time.monotonic() + 30
        while quitter.poll() is None:
            process.poll()
            if time.monotonic() > quit_deadline:
                quitter.kill()
                raise RuntimeError('dropmorph --quit did not finish.')
            time.sleep(0.05)
        if quitter.returncode != 0:
            raise RuntimeError(f'dropmorph --quit exited with {quitter.returncode}.')
        if process.wait(timeout=15) != 0:
            raise RuntimeError('The desktop app did not exit cleanly.')
        if is_linux:
            check_input_hook()
    except BaseException:
        log.flush()
        print(log_path.read_text(errors='replace'), file=sys.stderr)
        raise
    finally:
        if process.poll() is None:
            os.killpg(process.pid, signal.SIGTERM)
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
