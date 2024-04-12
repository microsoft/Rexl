BLD="$1"
if [[ -z "$BLD" ]]; then
    BLD="Debug"
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "$0")" >/dev/null 2>&1; pwd -P)"
SRC_DIR="$(cd -- "$(dirname -- "$0")" >/dev/null 2>&1; cd ../.. >/dev/null 2>&1; pwd -P)"
# echo "$SRC_DIR"

EDIR="$SRC_DIR/xout/bin/RexlKernel/x64/$BLD/net6.0"
# echo "EDIR: $EDIR"

if [ ! -d "$EDIR" ]; then
    echo "ERROR: Bld directory '$EDIR' does not exist"
    exit 1
fi

"$EDIR/Microsoft.RexlKernel" register
