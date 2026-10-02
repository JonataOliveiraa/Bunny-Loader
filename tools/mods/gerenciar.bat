@echo off
rem Abre o gerenciador do catalogo de mods no navegador.
cd /d "%~dp0..\.."
python tools\mods\manager.py
pause
