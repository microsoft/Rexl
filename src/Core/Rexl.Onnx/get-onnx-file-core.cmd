set CMD_DIR=%~dp0
echo %CMD_DIR%

set SRC_URL=%1
set FILE=%2

set DST_DIR=%CMD_DIR%..\..\xtmp\
set DST_FILE=%DST_DIR%%FILE%

if not exist "%DST_FILE%" (
    if not exist "%DST_DIR%" (
        md %DST_DIR%
        if ERRORLEVEL 1 exit 1
    )

    curl -o "%DST_FILE%" -L %SRC_URL%
    if ERRORLEVEL 1 exit 1
)
