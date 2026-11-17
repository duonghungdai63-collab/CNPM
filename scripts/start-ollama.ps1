param([switch]$DockerAccess)
# Chạy Ollama cục bộ cho chức năng dịch tự động; tắt cloud, không cần API key.
$env:OLLAMA_NO_CLOUD = "1"
if ($DockerAccess) { $env:OLLAMA_HOST = "0.0.0.0:11434" }  # chỉ dùng khi container api cần truy cập
$model = if ($env:OLLAMA_MODEL) { $env:OLLAMA_MODEL } else { "translategemma:4b" }
ollama pull $model
ollama serve
