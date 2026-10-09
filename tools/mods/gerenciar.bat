@echo off
rem Abre o gerenciador do catalogo de mods no navegador.
title Bunny Loader - Catalogo de mods
cd /d "%~dp0..\.."
where python >nul 2>nul || (echo Python nao encontrado no PATH. & pause & exit /b 1)
python tools\mods\manager.py
pause
