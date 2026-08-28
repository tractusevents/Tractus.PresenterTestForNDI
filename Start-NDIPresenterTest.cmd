@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-NDIPresenterTest.ps1" %*
pause
