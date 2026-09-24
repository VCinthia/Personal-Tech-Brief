@echo off
REM Double-click to shut the app down when you are done (your data is kept).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\stop-brief.ps1"
