@echo off
setlocal
cd /d "%~dp0..\.."
if not "%~1"=="" goto argumentos
python -X utf8 tools\mods\tmod.py --ask
goto fim
:argumentos
python -X utf8 tools\mods\tmod.py %*
:fim
set "tmod_result=%errorlevel%"
echo.
pause
exit /b %tmod_result%
