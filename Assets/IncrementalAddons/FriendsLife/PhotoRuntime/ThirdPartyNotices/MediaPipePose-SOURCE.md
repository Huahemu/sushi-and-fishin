# MediaPipe pose model, OpenCV Zoo ONNX conversion

Source: https://github.com/opencv/opencv_zoo/tree/main/models/pose_estimation_mediapipe
Model: pose_estimation_mediapipe_2023mar.onnx, 5,557,238 bytes, retrieved 2026-10-26 from official media.githubusercontent.com/opencv/opencv_zoo. The official directory README and included LICENSE state Apache 2.0 for its contents.

This local CPU-only dependency estimates 33 landmarks independently in four renders of the same generated GLB. Known orthographic cameras triangulate those estimates. It does not upload images, request a new reconstruction, contain original game models, or enter the transported character JSON. Pose estimates and the fitted native body are inferred. Covered anatomy and missing surface connections are not observed photo recovery. The original generated surface, UV atlas and source photo are retained in new immutable packages.

Runtime uses the existing Microsoft ONNX Runtime CPU dependency, one inference thread and the shared one-analysis semaphore. Original OpenCV Python wrapper and model README are archived in the task photo/pose directory as implementation provenance. The C# preprocessing follows normalized RGB NHWC input and output landmark coordinates; its four-view reprojection residual is checked before adaptation.
