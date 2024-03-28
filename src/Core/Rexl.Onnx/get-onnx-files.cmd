set CMD_DIR=%~dp0
echo %CMD_DIR%

call %CMD_DIR%\get-onnx-file-core.cmd https://github.com/onnx/models/raw/main/validated/vision/classification/resnet/model/resnet50-v2-7.onnx resnet50-v2-7.onnx
if ERRORLEVEL 1 exit 1

call %CMD_DIR%\get-onnx-file-core.cmd https://github.com/onnx/models/raw/main/validated/vision/classification/efficientnet-lite4/model/efficientnet-lite4-11.onnx efficientnet-lite4-11.onnx
if ERRORLEVEL 1 exit 1
