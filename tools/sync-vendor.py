#!/usr/bin/env python3
"""Vendor sync for the AI.Erp fork of WebVella-ERP.

The fork renames the vendor brand (WebVella -> AI). That rename is a one way
transform: it is safe going forward, because "WebVella" is a long unique token,
and unsafe going back, because "AI" is a two letter sequence that appears inside
ordinary words (CONTAINS, DOMAIN, MAINTAIN) and inside minified JS and base64.

So this tool never rewrites the whole tree. It takes the vendor delta and pushes
it forward through the transform. The invariant it maintains is:

    ours == transform(upstream@LAST_SYNC) + our_own_work

Commands
    check    Show what would be synced. Changes nothing.
    apply    Transform the vendor delta and apply it to the working tree.
    verify   Report how our tree relates to transform(upstream@LAST_SYNC).

State lives in .vendor-sync at the repo root.
"""
import argparse
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STATE_FILE = os.path.join(ROOT, '.vendor-sync')
UPSTREAM_REMOTE = 'upstream'

# ---------------------------------------------------------------- transform --

# Copied through unchanged. Never rewrite these.
PROTECT = re.compile(b'|'.join([
    rb'github\.com/WebVella[\w./\-]*',   # upstream repo URLs
    rb'WebVella\.TagHelpers',            # external NuGet package and namespace
    rb'WebVella\.Controls',              # external tag helper namespace
    rb'WebVellaErpWebComponents',       # JS globals in prebuilt Stencil bundles
    rb'WebVella-',                      # upstream repo names (WebVella-ERP, ...)
    rb'<[Aa]uthors>WebVella',           # package attribution metadata
    rb'<[Cc]ompany>WebVella',
    rb'<[Oo]wners>WebVella',
]))

# Paths never transformed. The AgentApi plugin is brand neutral by design: it
# binds to either brand at build time through ErpFlavor and using aliases.
# Rewriting its vendor ItemGroup would destroy the dual target.
SKIP_PATH_PREFIXES = (
    'AI.Erp.Plugins.AgentApi/',
    'tools/sync-vendor.py',
    '.vendor-sync',
)


def transform_bytes(data):
    out = []
    pos = 0
    for m in PROTECT.finditer(data):
        out.append(_sub(data[pos:m.start()]))
        out.append(m.group(0))
        pos = m.end()
    out.append(_sub(data[pos:]))
    return b''.join(out)


def _sub(s):
    s = s.replace(b'WebVella.ERP3', b'AI.Erp')
    s = s.replace(b'WebVella.ERP', b'AI.Erp')
    return s.replace(b'WebVella', b'AI')


def _skip_path(p):
    p = p.replace('\\', '/')
    return any(p.startswith(pref) for pref in SKIP_PATH_PREFIXES)


def transform_patch(data):
    """Transform a git patch, leaving binary payload sections untouched.

    A base85 payload line never contains a space, so any line that starts with a
    patch keyword is structurally guaranteed not to be payload.
    """
    lines = data.split(b'\n')
    out = []
    i = 0
    n = len(lines)
    while i < n:
        line = lines[i]
        if line.startswith(b'GIT binary patch'):
            out.append(line)
            i += 1
            if i < n:
                out.append(lines[i])          # literal <len> / delta <len>
                i += 1
            while i < n:
                cur = lines[i]
                if (not cur or cur.startswith(b'diff --git')
                        or cur.startswith(b'index ')
                        or cur.startswith(b'--- ')
                        or cur.startswith(b'+++ ')):
                    break
                out.append(cur)              # payload, untouched
                i += 1
            continue
        out.append(transform_bytes(line))
        i += 1
    return b'\n'.join(out)


# -------------------------------------------------------------------- git ---

def git(*args, binary=False, check=True, cwd=ROOT):
    r = subprocess.run(['git'] + list(args), cwd=cwd, capture_output=True)
    if check and r.returncode != 0:
        sys.stderr.write(r.stderr.decode(errors='replace'))
        raise SystemExit('git %s failed' % ' '.join(args))
    return r.stdout if binary else r.stdout.decode(errors='replace').strip()


# ------------------------------------------------------------------ state ---

def load_state():
    if not os.path.exists(STATE_FILE):
        return None
    for raw in open(STATE_FILE, encoding='utf-8'):
        raw = raw.strip()
        if raw.startswith('synced_from:'):
            return raw.split(':', 1)[1].strip()
    return None


def save_state(sha):
    with open(STATE_FILE, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('# Upstream commit this fork is currently synced to.\n')
        fh.write('# Updated by tools/sync-vendor.py apply. Do not edit by hand.\n')
        fh.write('synced_from: %s\n' % sha)


# ------------------------------------------------------- dependency graph ---

def dep_graph():
    import xml.etree.ElementTree as ET
    refs = {}
    for root, dirs, files in os.walk(ROOT):
        dirs[:] = [d for d in dirs if d not in ('.git', 'bin', 'obj', 'node_modules')]
        for f in files:
            if not f.endswith('.csproj'):
                continue
            full = os.path.join(root, f)
            rel = os.path.relpath(full, ROOT).replace('\\', '/')
            deps = []
            try:
                tree = ET.parse(full)
            except Exception:
                refs[rel] = []
                continue
            for el in tree.iter():
                tag = el.tag.split('}')[-1] if isinstance(el.tag, str) else el.tag
                if tag != 'ProjectReference':
                    continue
                inc = el.get('Include') or ''
                if not inc:
                    continue
                norm = os.path.normpath(os.path.join(root, inc))
                deps.append(os.path.relpath(norm, ROOT).replace('\\', '/'))
            refs[rel] = deps
    return refs


def project_of_path(path, refs):
    for p in refs:
        if path == p or path.startswith(os.path.dirname(p) + '/'):
            return p
    return None


def reverse_closure(paths, refs):
    rev = {}
    for p, ds in refs.items():
        for d in ds:
            rev.setdefault(d, []).append(p)
    need = set()
    frontier = [project_of_path(p, refs) for p in paths]
    frontier = [f for f in frontier if f]
    while frontier:
        cur = frontier.pop()
        for dependent in rev.get(cur, []):
            if dependent not in need:
                need.add(dependent)
                frontier.append(dependent)
    return need


# --------------------------------------------------------------- commands ---

def cmd_check(args):
    last = load_state()
    if not last:
        print('No .vendor-sync state. Run: python tools/sync-vendor.py init <sha>')
        return 1
    git('fetch', UPSTREAM_REMOTE, '--tags')
    tip = git('rev-parse', '%s/HEAD' % UPSTREAM_REMOTE) if _has_head() else \
        git('rev-parse', '%s/master' % UPSTREAM_REMOTE)
    behind = git('rev-list', '--count', '%s..%s' % (last, tip))
    print('synced from : %s' % last)
    print('upstream tip: %s' % tip)
    print('commits behind: %s' % behind)
    if behind == '0':
        print('Already up to date.')
        return 0
    raw = git('diff', '--binary', '%s..%s' % (last, tip), binary=True)
    files = _patch_files(raw)
    print('\n%d files changed upstream' % len(files))
    refs = dep_graph()
    touched = sorted({p for p in (project_of_path(f, refs) for f in files) if p})
    print('projects touched:')
    for t in touched:
        print('   %s' % t)
    extra = reverse_closure(files, refs) - set(touched)
    if extra:
        print('also need rebuild (dependents):')
        for e in sorted(extra):
            print('   %s' % e)
    skipped = [f for f in files if _skip_path(transform_bytes(f.encode()).decode())]
    if skipped:
        print('\nNOTE: %d upstream paths map onto skip-listed paths: %s'
              % (len(skipped), ', '.join(skipped[:5])))
    return 0


def _has_head():
    r = subprocess.run(['git', 'rev-parse', '--verify', '%s/HEAD' % UPSTREAM_REMOTE],
                      cwd=ROOT, capture_output=True)
    return r.returncode == 0


def _patch_files(patch):
    out = []
    for m in re.finditer(rb'^diff --git a/(\S+) b/(\S+)$', patch, re.M):
        out.append(m.group(2).decode('utf-8', 'replace'))
    return out


def cmd_apply(args):
    last = load_state()
    if not last:
        print('No .vendor-sync state. Run init first.')
        return 1
    git('fetch', UPSTREAM_REMOTE, '--tags')
    tip = git('rev-parse', '%s/master' % UPSTREAM_REMOTE)
    if tip == last:
        print('Already up to date.')
        return 0

    raw = git('diff', '--binary', '%s..%s' % (last, tip), binary=True)
    patch = transform_patch(raw)
    tmp = os.path.join(ROOT, '.qwen', 'vendor.rebranded.patch')
    os.makedirs(os.path.dirname(tmp), exist_ok=True)
    open(tmp, 'wb').write(patch)

    files = _patch_files(raw)
    blocked = [f for f in files if _skip_path(transform_bytes(f.encode()).decode())]
    if blocked and not args.force:
        print('Refusing: upstream touches paths that are on the never-transform list:')
        for b in blocked:
            print('   %s' % b)
        print('Review them by hand, or pass --force.')
        return 1

    chk = subprocess.run(['git', 'apply', '--check', '-p1', tmp],
                        cwd=ROOT, capture_output=True)
    if chk.returncode != 0:
        print('Transformed patch does not apply cleanly:')
        sys.stdout.write(chk.stderr.decode(errors='replace')[:3000])
        print('\nPatch kept at %s for inspection.' % tmp)
        return 1
    subprocess.run(['git', 'apply', '-p1', tmp], cwd=ROOT, check=True)
    save_state(tip)
    print('Applied %d upstream files. .vendor-sync now at %s' % (len(files), tip))
    print('Next: build and review, then commit.')
    return 0


def cmd_verify(args):
    last = load_state()
    if not last:
        print('No .vendor-sync state.')
        return 1
    up = _tree_at(last)
    ours = _tree_at('HEAD')
    in_sync, diverged, only_ours, only_up = [], [], [], []
    for k in sorted(set(up) | set(ours)):
        if k not in ours:
            only_up.append(k)
        elif k not in up:
            only_ours.append(k)
        elif up[k] == ours[k]:
            in_sync.append(k)
        else:
            diverged.append(k)
    print('compared HEAD against transform(upstream@%s)' % last)
    print('  in sync        : %d' % len(in_sync))
    print('  our additions : %d' % len(only_ours))
    print('  deleted by us : %d' % len(only_up))
    print('  diverged      : %d' % len(diverged))
    if diverged:
        print('\nDiverged files (our own edits, expected for owned code):')
        for d in diverged[:60]:
            print('   ~ %s' % d)
    return 0


def _tree_at(ref):
    tmp = os.path.join(ROOT, '.qwen', '_tree')
    if os.path.exists(tmp):
        _rmtree(tmp)
    os.makedirs(tmp)
    blob = git('archive', '--format=tar', ref, binary=True)
    subprocess.run(['tar', '-x', '-C', tmp], input=blob, check=True)
    out = {}
    for root, dirs, files in os.walk(tmp):
        dirs[:] = [d for d in dirs if d not in ('.git', 'bin', 'obj')]
        for f in files:
            full = os.path.join(root, f)
            rel = os.path.relpath(full, tmp).replace('\\', '/')
            data = open(full, 'rb').read()
            if b'\x00' in data[:8192]:
                data = b'<binary>'
            if _skip_path(rel):
                # Never transformed, but still compared, so owned code shows up
                # in the report instead of disappearing from it.
                out[rel] = data
            else:
                out[transform_bytes(rel.encode()).decode()] = transform_bytes(data)
    _rmtree(tmp)
    return out


def _rmtree(path):
    import shutil
    import stat as _stat

    def onerr(func, p, exc):
        try:
            os.chmod(p, _stat.S_IWRITE)
        except OSError:
            pass
        func(p)

    shutil.rmtree(path, onerror=onerr)


def cmd_init(args):
    save_state(args.sha)
    print('.vendor-sync set to %s' % args.sha)
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                               formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    sub.add_parser('check')
    p = sub.add_parser('apply')
    p.add_argument('--force', action='store_true')
    sub.add_parser('verify')
    p = sub.add_parser('init')
    p.add_argument('sha')
    args = ap.parse_args()
    return {'check': cmd_check, 'apply': cmd_apply,
            'verify': cmd_verify, 'init': cmd_init}[args.cmd](args)


if __name__ == '__main__':
    sys.exit(main())
