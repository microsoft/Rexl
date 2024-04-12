@echo off
setlocal

set BLD=%1

if "%BLD%"=="" (
    set BLD=Debug
)

set EDIR=%~dp0../../xout/bin/RexlKernel/x64/%BLD%/net6.0/
set EDIR=%EDIR:\=/%

if not exist "%EDIR%" (
    echo ERROR: Bld directory '%EDIR%' does not exist
    exit /b 1
)

@echo on

%EDIR%/Microsoft.RexlKernel register
