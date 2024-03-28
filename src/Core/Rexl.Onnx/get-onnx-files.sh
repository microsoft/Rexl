SCRIPT_DIR="$(cd -- "$(dirname -- "$0")" >/dev/null 2>&1; pwd -P)"

"$SCRIPT_DIR/get-onnx-file-core.sh" https://github.com/onnx/models/raw/main/validated/vision/classification/resnet/model/resnet50-v2-7.onnx resnet50-v2-7.onnx
STAT=$?
if [ "$STAT" -ne "0" ]; then
    echo "ERROR: Downloading resnet model failed"
    exit 1
fi

"$SCRIPT_DIR/get-onnx-file-core.sh" https://github.com/onnx/models/raw/main/validated/vision/classification/efficientnet-lite4/model/efficientnet-lite4-11.onnx efficientnet-lite4-11.onnx
STAT=$?
if [ "$STAT" -ne "0" ]; then
    echo "ERROR: Downloading efficient model failed"
    exit 1
fi
