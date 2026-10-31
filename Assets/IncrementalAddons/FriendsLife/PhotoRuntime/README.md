# FriendsLife photo CPU appearance processing

This directory contains only the added photo-processing runtime dependencies. It does not replace game models, scenes, lighting settings or the original character editor.

- Microsoft ONNX Runtime managed/native CPU version1.31.0, reused from the existing local probe payload. Upstream: https://github.com/microsoft/onnxruntime ; API: https://onnxruntime.ai/docs/api/csharp/api/Microsoft.ML.OnnxRuntime.html . Microsoft MIT license and upstream notices are retained in ThirdPartyNotices.
- Quantized ONNX clothes parser: https://huggingface.co/Xenova/segformer_b2_clothes ; base model: https://huggingface.co/mattmdjaga/segformer_b2_clothes . The base model's license links https://github.com/NVlabs/SegFormer/blob/master/LICENSE and limits use to research/evaluation noncommercial purposes. The full linked license is retained in ThirdPartyNotices/SegFormer-LICENSE.txt.

Resources/FriendsLife/Photo/SegformerClothes.bytes is the already downloaded quantized model, with its existing model/preprocessor metadata. The application uses dynamic384 inputs, four actual model-view renders plus a head-detail crop where the parser locates the head, then projects visible labels to that model's own UV atlas. No person identity, age, personality or preference is inferred.

Windows64 CPU only in this integration. The library is loaded only when a photo-processing job starts. ORT thread count is1 and concurrent model tasks are limited to1; sessions are disposed after each job. Other game workloads' thread, GPU and clock settings are untouched.

The native import metadata enables Windows Editor and Win64 Player. The native library is explicitly loaded from this added directory (Editor) or the Player's Plugins/x86_64 directory before constructing the managed session, so an OS-supplied older onnxruntime.dll is not selected accidentally.
