import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('log', type=Path)
args = parser.parse_args()
records = {}
for line in args.log.read_text(encoding='utf-8').splitlines():
    for event in ['GENPASS_GENERATED', 'GENPASS_SAVED', 'GENPASS_RELOADED']:
        if event + ' ' in line:
            records[event] = json.loads(line.split(event + ' ', 1)[1])

assert set(records) == {'GENPASS_GENERATED', 'GENPASS_SAVED', 'GENPASS_RELOADED'}, records.keys()
generated, saved, reloaded = (records[event] for event in ['GENPASS_GENERATED', 'GENPASS_SAVED', 'GENPASS_RELOADED'])
assert generated['passed'] and reloaded['passed'] and saved['valid']
assert not generated['failures'] and not saved['failures'] and not reloaded['failures']
assert generated['creatorThread'] != generated['generationThread']
assert generated['managedThread'] == 'worldGenCallback'
assert generated['nativeExecuted'] > 50 and min(generated['size']) >= 1200
assert generated['nativeExecuted'] == generated['nativePasses']
assert generated['worldName'] == reloaded['worldName']
assert generated['subclassRuns'] == 1 and generated['legacyRuns'] == 1 and generated['disabledRuns'] == 0
assert generated['events'] == ['pre', 'genpass-before', 'genpass-structure', 'passlegacy-after', 'post']
assert generated['matchingTiles'] == saved['matchingTiles'] == reloaded['matchingTiles'] == 35
assert reloaded['generationCallbacksThisProcess'] == 0
print(json.dumps({'nativeExecuted': generated['nativeExecuted'], 'size': generated['size'],
                  'matchingTilesAfterRestart': reloaded['matchingTiles'], 'passed': True}))
