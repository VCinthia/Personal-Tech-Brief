@echo off
REM Double-click to get your brief: starts the app, fetches fresh updates, and opens the brief.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\brief.ps1"
