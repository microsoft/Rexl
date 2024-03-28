SRC_DIR="$(cd -- "$(dirname -- "$0")" >/dev/null 2>&1; cd ../.. >/dev/null 2>&1; pwd -P)"

SRC_URL=$1
FILE=$2

DST_DIR=$SRC_DIR/xtmp
DST_FILE=$DST_DIR/$FILE

mkdir "$DST_DIR" -p

if [ ! -f "$DST_FILE" ]; then
    mkdir "$DST_DIR" -p
    curl -o "$DST_FILE" -L $SRC_URL
    STAT=$?
    if [ "$STAT" -ne "0" ]; then
        echo "ERROR: Downloading file failed"
        exit 1
    fi
fi
