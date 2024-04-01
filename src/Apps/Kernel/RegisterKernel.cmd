@echo off
setlocal

set BLD=%1

if "%BLD%"=="" (
    set BLD=Debug
)

set EDIR=%~dp0../../xout/bin/RexlKernel/x64/%BLD%/net6.0/
set EDIR=%EDIR:\=/%

echo EDIR: %EDIR%

if not exist "%EDIR%" (
    echo ERROR: Bld directory '%EDIR%' does not exist
    exit /b 1
)

set DDIR=.krn-spec-%BLD%
set DST=%DDIR%\kernel.json

md %DDIR%

echo { > %DST%
echo     "argv": ["%EDIR%Microsoft.RexlKernel.exe", "{connection_file}"], >> %DST%
echo     "display_name": "Rexl", >> %DST%
echo     "language": "Rexl" >> %DST%
echo } >> %DST%

@echo on

jupyter kernelspec install %DDIR% --name=rexl

jupyter kernelspec list --json
