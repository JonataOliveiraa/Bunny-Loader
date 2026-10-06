import json
import math
import re
import argparse
from pathlib import Path


def read(path, count):
    source = path.read_text(encoding='utf-8', errors='replace')
    if re.search(r'Fatal signal|exception in callback|error in \[', source):
        raise ValueError('Erro de runtime em ' + str(path))
    rows = []
    for line in source.splitlines():
        if 'hookperf ' not in line:
            continue
        row = json.loads(line.split('hookperf ', 1)[1])
        row['classes'] = row.pop('players', row.get('classes', count))
        rows.append(row)
        for hook in row.pop('hooks', []) if row['kind'] == 'phase' else []:
            rows.append(dict(kind='hook', classes=row['classes'], phase=row['phase'], **hook))
    return rows


parser = argparse.ArgumentParser(description='Confere fases, callbacks e filtros do benchmark nativo.')
parser.add_argument('logs', nargs='+', type=Path)
parser.add_argument('--classes', type=int, choices=[0, 1, 8])
args = parser.parse_args()
counts = [0, 1, 8] if args.classes is None else [args.classes]
if len(args.logs) != len(counts):
    parser.error('Informe log-0 log-1 log-8, ou um unico log com --classes.')
records = []
for path, count in zip(args.logs, counts):
    rows = read(path, count)
    if not rows or any(row['classes'] != count for row in rows):
        raise ValueError('Quantidade de classes incorreta em ' + str(path))
    contexts = [row for row in rows if row['kind'] == 'context']
    ends = [row for row in rows if row['kind'] == 'FIM']
    phases = [row for row in rows if row['kind'] == 'phase']
    expected = ['ticks_8', 'draw_empty', 'draw_helper', 'draw_hide', 'draw_reorder', 'draw_empty_repeat'] if count == 8 else ['ticks_' + str(count)]
    if len(contexts) != 1 or contexts[0]['netMode'] != 0 or len(ends) != 1 or ends[0]['phases'] != len(expected):
        raise ValueError('Execucao incompleta ou fora de singleplayer')
    if [row['phase'] for row in phases] != expected:
        raise ValueError('Fases ausentes ou duplicadas')
    for phase in phases:
        if phase['callbacks'] != count * 15 * 300 or phase['update']['count'] != 300:
            raise ValueError('Contagem de callbacks/updates inesperada')
        for name in ['update', 'draw']:
            stats = phase[name]
            if stats['count'] <= 0 or any(not math.isfinite(stats[key]) or stats[key] < 0 for key in ['meanMs', 'medianMs', 'p95Ms', 'maxMs']):
                raise ValueError('Estatistica invalida')
        hooks = [row for row in rows if row['kind'] == 'hook' and row['phase'] == phase['phase']]
        layers = [row for row in hooks if row['name'].startswith('PlayerDrawLayers.DrawPlayer_')]
        if phase['phase'] in ['draw_empty', 'draw_helper', 'draw_empty_repeat'] and sum(row['js'] for row in layers) != 0:
            raise ValueError('Camadas sem alteracao entraram no JavaScript')
        if phase['phase'] in ['draw_hide', 'draw_reorder'] and not sum(row['js'] for row in layers):
            raise ValueError('Alteracao de camadas nao foi exercitada')
    filters = {row['name']: row for row in rows if row['kind'] == 'filter'}
    for item in ['vanilla', 'plain']:
        if filters['weapon_damage_' + item]['js'] != 0 or filters['weapon_damage_' + item]['callbacks'] != 0:
            raise ValueError('Filtro de dano falhou')
        if filters['player_frame_' + item]['callbacks'] != 0 or any(hook['js'] for hook in filters['player_frame_' + item]['hooks']):
            raise ValueError('Filtro de frames falhou')
    if filters['weapon_damage_active']['callbacks'] != 7100 or filters['weapon_damage_active']['js'] != 7100:
        raise ValueError('Modificador ativo nao foi exercitado')
    if filters['player_frame_active']['callbacks'] != 3600:
        raise ValueError('Frame ativo nao foi exercitado')
    records.extend(rows)
    print(str(count) + ' classes: fases, callbacks, amostras e filtros conferidos')
print(json.dumps({'updates': sum(row['update']['count'] for row in records if row['kind'] == 'phase'),
                  'draws': sum(row['draw']['count'] for row in records if row['kind'] == 'phase')}, ensure_ascii=False))
