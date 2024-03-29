BLD="$1"
if [[ -z "$BLD" ]]; then
    BLD="Debug"
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "$0")" >/dev/null 2>&1; pwd -P)"
SRC_DIR="$(cd -- "$(dirname -- "$0")" >/dev/null 2>&1; cd .. >/dev/null 2>&1; pwd -P)"
# echo "$SRC_DIR"

EDIR="$SRC_DIR/xout/bin/RexlKernel/x64/$BLD/net6.0"
# echo "EDIR: $EDIR"

if [ ! -d "$EDIR" ]; then
    echo "ERROR: Bld directory '$EDIR' does not exist"
    exit 1
fi

DDIR="$SCRIPT_DIR/.krn-spec-$BLD"
# echo "DDIR: $DDIR"

DST="$DDIR/kernel.json"
# echo "DST: $DST"

mkdir "$DDIR" -p

echo { > "$DST"
echo     \"argv\": [\"$EDIR/Microsoft.RexlKernel\", \"{connection_file}\"], >> "$DST"
echo     \"display_name\": \"Rexl\", >> "$DST"
echo     \"language\": \"Rexl\" >> "$DST"
echo } >> "$DST"

# cat "$DST"

jupyter kernelspec install --user "$DDIR" --name=rexl

jupyter kernelspec list --json
