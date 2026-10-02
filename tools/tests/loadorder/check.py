"""Confere os quatro mods de ordem na sessao atual, sem alterar o aparelho."""
import argparse
import re
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--device', required=True, help='Serial adb do aparelho de teste')
    parser.add_argument('--expected', required=True, help='Ordem esperada, por exemplo ABCD ou DCBA')
    args = parser.parse_args()
    expected = args.expected.upper()
    if sorted(expected) != list('ABCD'):
        parser.error('expected deve conter A, B, C e D uma vez cada')
    def adb(*command, allow_empty=False):
        result = subprocess.run(['adb', '-s', args.device, *command], capture_output=True,
                                text=True, encoding='utf-8', errors='replace')
        if result.returncode and not (allow_empty and not result.stdout and not result.stderr):
            raise SystemExit(result.stderr.strip() or 'Falha ao consultar o aparelho pelo adb.')
        return result.stdout
    pid = adb('shell', 'pidof', 'com.bunnyloader:game', allow_empty=True).strip()
    if not pid:
        raise SystemExit('Abra um mundo com os quatro mods antes de conferir.')
    log = adb('logcat', '-d', '--pid='+pid, '-s', 'BunnyLoader')
    matches = re.findall(r'loadorder FIM: (ok|FALHOU) LOAD=([A-D>]+) BEFORE=([A-D>]+) AFTER=([A-D>]+)', log)
    if not matches:
        raise SystemExit('Ainda nao houve loadorder FIM nesta sessao.')
    status, loaded, before, after = matches[-1]
    print(f'{status}: LOAD={loaded} BEFORE={before} AFTER={after}')
    if status != 'ok' or loaded.replace('>', '') != expected or before != loaded or after.replace('>', '') != expected[::-1]:
        raise SystemExit('A ordem real diverge da esperada.')


if __name__ == '__main__':
    main()
