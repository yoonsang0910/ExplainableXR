using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR.ARFoundation;
using Unity.Collections;
using UnityEngine.XR.ARSubsystems;

namespace ExplainableXR
{
    using static ExplainableXR.Utility;
    public class ImageRecordManager
    {
        private MonoBehaviour targetMono = null;
        private string dirName = null;
        private string depthDirName = null;
        private string confidenceDirName = null;
        private string metaDirName = null;
        private RenderTexture renderTexture = null;
        private int imageIndex = 1;
        private Queue<string> imageIndexQueue = new Queue<string>();
        private Camera mainCam, colorCam, depthCam;
        private ARCameraManager arCameraManager;
        private AROcclusionManager occlusionManager;
        private Texture2D colorTexture, depthTexture;
        public ImageRecordManager(MonoBehaviour monoBehaviour, Camera snapshotCam, string rootDirName)
        {
            dirName = Path.Combine(rootDirName, "Image");
            depthDirName = Path.Combine(rootDirName, "Depth");
            confidenceDirName = Path.Combine(rootDirName, "Confidence");
            metaDirName = Path.Combine(rootDirName, "Camera");
            var contextDirName = Path.Combine(rootDirName, "Context");

            Directory.CreateDirectory(dirName);
            Directory.CreateDirectory(depthDirName);
            Directory.CreateDirectory(confidenceDirName);
            Directory.CreateDirectory(metaDirName);
            Directory.CreateDirectory(contextDirName);
            targetMono = monoBehaviour;
            imageIndex = 1;
            mainCam = snapshotCam;
            CreateColorCamera();
            CreateDepthCamera();
            depthTexture = new Texture2D(depthCam.pixelWidth, depthCam.pixelHeight, TextureFormat.RFloat, false);
            depthTexture.filterMode = FilterMode.Point;
            colorTexture = new Texture2D(colorCam.pixelWidth, colorCam.pixelHeight, TextureFormat.ARGB32, false);
            colorTexture.filterMode = FilterMode.Point;

            // renderTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            // // renderTexture_flipped = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        }
        public ImageRecordManager(MonoBehaviour monoBehaviour, Camera snapshotCam, ARCameraManager arCameraManager, AROcclusionManager occlusionManager, string rootDirName)
        {
            this.arCameraManager = arCameraManager;
            this.occlusionManager = occlusionManager;
            dirName = Path.Combine(rootDirName, "Image");
            depthDirName = Path.Combine(rootDirName, "Depth");
            confidenceDirName = Path.Combine(rootDirName, "Confidence");
            metaDirName = Path.Combine(rootDirName, "Camera");
            var contextDirName = Path.Combine(rootDirName, "Context");

            Directory.CreateDirectory(dirName);
            Directory.CreateDirectory(depthDirName);
            Directory.CreateDirectory(confidenceDirName);
            Directory.CreateDirectory(metaDirName);
            Directory.CreateDirectory(contextDirName);
            targetMono = monoBehaviour;
            imageIndex = 1;
            mainCam = snapshotCam;
            CreateColorCamera();
            CreateDepthCamera();
            depthTexture = new Texture2D(depthCam.pixelWidth, depthCam.pixelHeight, TextureFormat.RGBAFloat, false);
            depthTexture.filterMode = FilterMode.Point;
            colorTexture = new Texture2D(colorCam.pixelWidth, colorCam.pixelHeight, TextureFormat.ARGB32, false);
            colorTexture.filterMode = FilterMode.Point;

            // renderTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            // // renderTexture_flipped = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        }
        public string TakePhysicalCameraScreenshot(bool recordDepth = true) //Recording AR RGB+D+MetaData
        {
            //For AR Camera
            var fileName = $"{imageIndex++}.png";
            targetMono.StartCoroutine(AR_ColorCamScreenshot(fileName));
            if (recordDepth)
                targetMono.StartCoroutine(AR_DepthCamScreenshot(fileName));
            return fileName;
        }

        private IEnumerator AR_ColorCamScreenshot(string fileName)
        {
            if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cameraImage))
            {
                MsgPrintError("Cannot acquire camera image");
                yield break;
            }

            var conversionParams = new XRCpuImage.ConversionParams()
            {
                inputRect = new RectInt(0, 0, cameraImage.width, cameraImage.height),
                // outputDimensions = new Vector2Int(cameraImage.width, cameraImage.height),
                outputDimensions = new Vector2Int(cameraImage.width/4, cameraImage.height/4), //iPad (1920x1440) => (480, 360)
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorX
            };

            SaveColorTextureAsPNG(cameraImage, conversionParams, fileName);

            //Save camera params (metadata)
            SaveCameraParameters(mainCam, arCameraManager, fileName);

            cameraImage.Dispose();
        }
        private void SaveColorTextureAsPNG(XRCpuImage cpuImage, XRCpuImage.ConversionParams conversionParams, string fileName)
        {
            // Create a NativeArray to hold the converted data
            int dataSize = cpuImage.GetConvertedDataSize(conversionParams);
            var rawTextureData = new NativeArray<byte>(dataSize, Allocator.Temp);

            // Convert the image to raw texture data
            cpuImage.Convert(conversionParams, rawTextureData);

            var pngData = ImageConversion.EncodeNativeArrayToPNG(rawTextureData, GraphicsFormat.R8G8B8A8_UNorm,
            (uint)conversionParams.outputDimensions.x, (uint)conversionParams.outputDimensions.y).ToArray();

            // Save the PNG data to a file
            if (pngData != null)
            {
                var dataSaveFilePath = Path.Combine(dirName, fileName);
                File.WriteAllBytes(dataSaveFilePath, pngData);
                MsgPrint($"Screen color snapshot saved to : {dataSaveFilePath}");
            }
            else
                MsgPrintError("Failed to encode texture to PNG");

            // Dispose of the NativeArray
            rawTextureData.Dispose();
        }


        private IEnumerator AR_DepthCamScreenshot(string fileName)
        {
            if (!occlusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage depthImage))
            {
                MsgPrintError("Cannot acquire depth image");
                yield break;
            }

            if (!occlusionManager.TryAcquireEnvironmentDepthConfidenceCpuImage(out XRCpuImage depthConfidenceImage))
            {
                MsgPrintError("Cannot acquire depth confidenc image");
                depthImage.Dispose();
                yield break;
            }

            // MsgPrint(string.Format(
            // "DepthImage info:\n\twidth: {0}\n\theight: {1}\n\tplaneCount: {2}\n\ttimestamp: {3}\n\tformat: {4}", 
            // depthImage.width, depthImage.height, depthImage.planeCount, depthImage.timestamp, depthImage.format));

            var conversionParams = new XRCpuImage.ConversionParams()
            {
                inputRect = new RectInt(0, 0, depthImage.width, depthImage.height),
                outputDimensions = new Vector2Int(depthImage.width, depthImage.height),
                outputFormat = TextureFormat.RFloat,
                transformation = XRCpuImage.Transformation.MirrorX
            };

            SaveDepthTextureAsPNG(depthImage, conversionParams, fileName);
            SaveDepthConfidenceTextureAsPNG(depthConfidenceImage, conversionParams, fileName);
            depthImage.Dispose();
            depthConfidenceImage.Dispose();
        }

        private void SaveDepthTextureAsPNG(XRCpuImage cpuImage, XRCpuImage.ConversionParams conversionParams, string fileName)
        {
            // Create a NativeArray to hold the converted data
            int dataSize = cpuImage.GetConvertedDataSize(conversionParams);
            var rawTextureData = new NativeArray<byte>(dataSize, Allocator.Temp);

            // Convert the image to raw texture data
            cpuImage.Convert(conversionParams, rawTextureData);

            var pngData = ImageConversion.EncodeNativeArrayToPNG(rawTextureData, GraphicsFormat.R32_SFloat,
            (uint)conversionParams.outputDimensions.x, (uint)conversionParams.outputDimensions.y).ToArray();

            // Save the PNG data to a file
            if (pngData != null)
            {
                var dataSaveFilePath = Path.Combine(depthDirName, fileName);
                File.WriteAllBytes(dataSaveFilePath, pngData);
                MsgPrint($"Screen depth snapshot saved to : {dataSaveFilePath}");
            }
            else
                MsgPrintError("Failed to encode texture to PNG");

            // Dispose of the NativeArray
            rawTextureData.Dispose();
        }
        private void SaveDepthConfidenceTextureAsPNG(XRCpuImage cpuImage, XRCpuImage.ConversionParams conversionParams, string fileName)
        {
            // Create a NativeArray to hold the converted data
            conversionParams.outputFormat = TextureFormat.R8;
            int dataSize = cpuImage.GetConvertedDataSize(conversionParams);
            var rawTextureData = new NativeArray<byte>(dataSize, Allocator.Temp);

            // Convert the image to raw texture data
            cpuImage.Convert(conversionParams, rawTextureData);

            var pngData = ImageConversion.EncodeNativeArrayToPNG(rawTextureData, GraphicsFormat.R8_UNorm,
            (uint)conversionParams.outputDimensions.x, (uint)conversionParams.outputDimensions.y).ToArray();

            // Save the PNG data to a file
            if (pngData != null)
            {
                var dataSaveFilePath = Path.Combine(confidenceDirName, fileName);
                File.WriteAllBytes(dataSaveFilePath, pngData);
                MsgPrint($"Screen depth confidence snapshot saved to : {dataSaveFilePath}");
            }
            else
                MsgPrintError("Failed to encode texture to PNG");

            // Dispose of the NativeArray
            rawTextureData.Dispose();
        }

        //Referent object is 
        public string TakeVirtualCameraScreenshot(object actionReferent = null) //Recording VR RGB+D+MetaData
        {
            var actionReferentGobj = actionReferent as GameObject;
            //ScreenCapture.CaptureScreenshot(dataSaveFilePath);
            var fileName = $"{imageIndex++}.png";
            // imageIndexQueue.Enqueue(fileName);
            targetMono.StartCoroutine(ColorCamScreenshot(fileName, actionReferentGobj));
            targetMono.StartCoroutine(DepthCamScreenshot(fileName, actionReferentGobj));
            return fileName;
        }
        private IEnumerator ColorCamScreenshot(string fileName = null, GameObject actionReferentGobj = null)
        {
            yield return new WaitForEndOfFrame();
            var curRT = RenderTexture.active;
            colorCam.enabled = true;
            RenderTexture.active = colorCam.targetTexture;
            actionReferentGobj?.SetActive(false);
            colorCam.Render();
            actionReferentGobj?.SetActive(true);
            colorTexture.ReadPixels(new Rect(0, 0, colorTexture.width, colorTexture.height), 0, 0, false);
            colorCam.enabled = false;
            SaveColorTextureAsPNG(colorTexture, fileName);

            //Save camera params (metadata)
            SaveCameraParameters(colorCam, fileName);
            RenderTexture.active = curRT;

            // ScreenCapture.CaptureScreenshotIntoRenderTexture(renderTexture);
            // //The images are flipped by Y (flip it during the post-processing step) : Y=(1-Y) or OpenCV FlipY
            // //Flipping it here is just additional overhead -> avoiding
            // Graphics.Blit(renderTexture, renderTexture_flipped, new Vector2(1, -1), new Vector2(0, 1));
            // yield return new WaitForEndOfFrame();
            // if (saveDepth)
            // {
            //     depthCam.enabled = true;
            //     var curRT = RenderTexture.active;
            //     RenderTexture.active = depthCam.targetTexture;
            //     depthCam.Render();
            //     depthTexture.ReadPixels(new Rect(0, 0, depthTexture.width, depthTexture.height), 0, 0, false);
            //     depthCam.enabled = false;
            //     SaveDepthTextureAsPNG(depthTexture, fileName);
            //     RenderTexture.active = curRT;
            // }
            // AsyncGPUReadback.Request(renderTexture_flipped, 0, TextureFormat.RGBA32, ReadbackCompleted);
        }
        private IEnumerator DepthCamScreenshot(string fileName = null, GameObject actionReferentGobj = null)
        {
            yield return new WaitForEndOfFrame();
            var curRT = RenderTexture.active;
            depthCam.enabled = true;
            RenderTexture.active = depthCam.targetTexture;
            actionReferentGobj?.SetActive(false);
            depthCam.Render();
            actionReferentGobj?.SetActive(true);
            depthTexture.ReadPixels(new Rect(0, 0, depthTexture.width, depthTexture.height), 0, 0, false);
            depthCam.enabled = false;
            SaveDepthTextureAsPNG(depthTexture, fileName);
            RenderTexture.active = curRT;
        }
        private void SaveColorTextureAsPNG(Texture2D texture, string fileName)
        {
            var pngBytes = texture.EncodeToPNG();
            var dataSaveFilePath = Path.Combine(dirName, fileName);
            File.WriteAllBytes(dataSaveFilePath, pngBytes);
            MsgPrint($"Screen color snapshot saved to : {dataSaveFilePath}");
        }
        private void SaveDepthTextureAsPNG(Texture2D texture, string fileName)
        {
            var dataSaveFilePath = Path.Combine(depthDirName, fileName);

            // // EXR (32bit) has higher precision than PNG
            // var exrBytes = texture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat); //32bit
            // // var exrBytes = texture.EncodeToEXR(Texture2D.EXRFlags.None); //16bit
            // File.WriteAllBytes(dataSaveFilePath + ".exr", exrBytes);

            // PNG bytes (8bit: 0-255)
            var pngBytes = texture.EncodeToPNG();
            File.WriteAllBytes(dataSaveFilePath, pngBytes);
            MsgPrint($"Screen depth snapshot saved to : {dataSaveFilePath}");
        }
        private void ReadbackCompleted(AsyncGPUReadbackRequest request)
        {
            using (var imageBytes = request.GetData<byte>())
            {
                var fileName = imageIndexQueue.Dequeue();
                var dataSaveFilePath = Path.Combine(dirName, fileName);
                var pngBytes = ImageConversion.EncodeNativeArrayToPNG(imageBytes, GraphicsFormat.R8G8B8A8_UNorm,
                    (uint)renderTexture.width, (uint)renderTexture.height);
                File.WriteAllBytes(dataSaveFilePath, pngBytes.ToArray());
                pngBytes.Dispose();
                MsgPrint($"Screen snapshot saved to : {dataSaveFilePath}");
            }
        }
        private void CreateColorCamera()
        {
            var colorCamGobj = new GameObject("Color Camera");
            colorCam = colorCamGobj.AddComponent<Camera>();
            colorCam.CopyFrom(mainCam);
            colorCam.transform.SetParent(mainCam.transform);

            colorCam.renderingPath = RenderingPath.Forward;
            colorCam.clearFlags = CameraClearFlags.SolidColor;
            // colorCam.backgroundColor = Color.clear;
            colorCam.backgroundColor = new Color(1, 1, 1, 0);
            colorCam.cullingMask = ~LayerMask.GetMask("PlayerBody");

            colorCam.farClipPlane = mainCam.farClipPlane;
            colorCam.depth = 100f;

            colorCam.targetTexture = CreateColorRenderTexture(mainCam);

            colorCam.useOcclusionCulling = false;
            colorCam.allowHDR = false;
            colorCam.allowMSAA = false;

            colorCam.enabled = false;
        }

        private RenderTexture CreateColorRenderTexture(Camera mainCam)
        {
            //Quest3 snapshot full res: (2016x1760) => (504x440)
            // RenderTexture rt = new RenderTexture(mainCam.pixelWidth / 1, mainCam.pixelHeight / 1, 24, RenderTextureFormat.ARGB32);
            RenderTexture rt = new RenderTexture(mainCam.pixelWidth / 4, mainCam.pixelHeight / 4, 24, RenderTextureFormat.ARGB32);
            rt.name = "Color RenderTexture";
            rt.useMipMap = false;
            rt.autoGenerateMips = false;
            rt.filterMode = FilterMode.Point;
            rt.hideFlags = HideFlags.DontSave;
            rt.enableRandomWrite = true;
            rt.Create();

            return rt;
        }

        private void CreateDepthCamera()
        {
            var depthCamGobj = new GameObject("Depth Camera");
            depthCam = depthCamGobj.AddComponent<Camera>();
            depthCam.CopyFrom(mainCam);
            depthCam.transform.SetParent(mainCam.transform);

            depthCam.depthTextureMode = DepthTextureMode.Depth;


            depthCam.renderingPath = RenderingPath.Forward;
            depthCam.clearFlags = CameraClearFlags.SolidColor;
            // depthCam.backgroundColor = Color.clear;
            depthCam.backgroundColor = new Color(1, 1, 1, 0);
            depthCam.cullingMask = ~LayerMask.GetMask("PlayerBody");

            depthCam.farClipPlane = mainCam.farClipPlane;
            depthCam.depth = -100f;

            depthCam.targetTexture = CreateDepthRenderTexture(mainCam);
            depthCam.SetReplacementShader(Resources.Load<Shader>("DepthShader"), "");

            depthCam.useOcclusionCulling = false;
            depthCam.allowHDR = false;
            depthCam.allowMSAA = false;

            depthCam.enabled = false;
        }

        private RenderTexture CreateDepthRenderTexture(Camera mainCam)
        {
            //Quest3 snapshot full res: (2016x1760) => (504x440)
            // RenderTexture rt = new RenderTexture(mainCam.pixelWidth / 1, mainCam.pixelHeight / 1, 24, RenderTextureFormat.RFloat);
            RenderTexture rt = new RenderTexture(mainCam.pixelWidth / 4, mainCam.pixelHeight / 4, 24, RenderTextureFormat.RFloat);
            rt.name = "Depth RenderTexture";
            rt.useMipMap = false;
            rt.autoGenerateMips = false;
            rt.filterMode = FilterMode.Point;
            rt.hideFlags = HideFlags.DontSave;
            rt.enableRandomWrite = true;
            rt.Create();

            return rt;
        }
        private void SaveCameraParameters(Camera cam, ARCameraManager arCameraManager, string fileName)
        {
            var dataSaveFilePath = Path.Combine(metaDirName, $"{fileName}.json");
            Utility.SaveCameraParameters(cam, arCameraManager, dataSaveFilePath);
            MsgPrint($"Screen camera metadata saved to : {dataSaveFilePath}");
        }
        private void SaveCameraParameters(Camera cam, string fileName)
        {
            var dataSaveFilePath = Path.Combine(metaDirName, $"{fileName}.json");
            Utility.SaveCameraParameters(cam, dataSaveFilePath);
            MsgPrint($"Screen camera metadata saved to : {dataSaveFilePath}");
        }

        private void LoadCameraParameters(Camera cam, string fileName)
        {
            var dataSaveFilePath = Path.Combine(metaDirName, $"{fileName}.json");
            if (Utility.LoadCameraParameters(cam, dataSaveFilePath))
                MsgPrint($"Screen camera metadata loaded from : {dataSaveFilePath}");
        }
    }
}