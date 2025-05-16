using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Security;
using System.Text;
using TMPro;
using UnityEngine;
using System.IO;
using UnityEngine.InputSystem.Interactions;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using GLTFast.Export;
using UnityEngine.Assertions;

namespace ExplainableXR
{
    public static class Utility
    {
        public const float DISCRETE_ACTION_DURATION_SEC = 0.5f; // Duration of every discrete action is defined as 0.5secs for dashboard vis. purpose
        public const float DEFAULT_CONTINUOUS_ACTION_DURATION_SEC = -1f;
        public static string GetDeviceUniqueID()
        {
            var newID = SystemInfo.deviceUniqueIdentifier;
            var deviceID = PlayerPrefs.GetString("DeviceID", newID);
            if (deviceID == newID)
                PlayerPrefs.SetString("DeviceID", deviceID);
            return deviceID;
        }

        private static readonly Dictionary<Type, string> typeMappings = new Dictionary<Type, string>()
        {
            { typeof(bool), "bool" },
            { typeof(byte), "byte" },
            { typeof(char), "char" },
            { typeof(decimal), "decimal" },
            { typeof(double), "double" },
            { typeof(float), "float" },
            { typeof(int), "int" },
            { typeof(long), "long" },
            { typeof(object), "object" },
            { typeof(sbyte), "sbyte" },
            { typeof(short), "short" },
            { typeof(string), "string" },
            { typeof(uint), "uint" },
            { typeof(ulong), "ulong" },
            { typeof(ushort), "ushort" }
        };
        public static string TypeToStringName(Type type)
        {
            if (typeMappings.TryGetValue(type, out string name))
                return name;
            else
                return type.Name;
        }

        public static void LogWrite(TMP_Text logObj, string msg)
        {
            logObj.text += $"{msg}\n";
        }
        public static void LogFlush(TMP_Text logObj)
        {
            logObj.text = "";
        }
        public static string Details(this Bounds bounds)
        {
            return $"center:{bounds.center.Details()}, " +
                $"size:{bounds.size.Details()}, " +
                $"min:{bounds.min.Details()}, " +
                $"max:{bounds.max.Details()}";
        }
        public static string Details(this Vector4 vec4)
        {
            return $"({vec4.x},{vec4.y},{vec4.z},{vec4.w})";
        }
        public static string Details(this Vector3 vec3)
        {
            return $"({vec3.x},{vec3.y},{vec3.z})";
        }
        public static string Details(this Vector2 vec2)
        {
            return $"({vec2.x},{vec2.y})";
        }
        public static Vector3 ToVector3(this string str)
        {
            var strList = str.Split(",");

            if (float.TryParse(strList[0], out var x) &&
                float.TryParse(strList[1], out var y) &&
                float.TryParse(strList[2], out var z))
            {
                return new Vector3(x, y, z);
            }
            throw new Exception("[ToVector3] Failed to convert to vector3");
        }
        public static string Unravel(this Vector3 pos)
        {
            return $"{pos.x},{pos.y},{pos.z}";
        }
        public static string Unravel(this Quaternion rot)
        {
            return $"{rot.x},{rot.y},{rot.z}";
        }
        public static string MergeVecQuatStr(string pos, string rot)
        {
            return $"{pos},{rot}";
        }
        public static string Unravel(this Transform transform)
        {
            var pos = transform.position;
            var rot = transform.rotation.eulerAngles;
            return $"{pos.x},{pos.y},{pos.z},{rot.x},{rot.y},{rot.z}";
        }
        public static string Unravel(this Pose pose)
        {
            var pos = pose.position;
            var rot = pose.rotation.eulerAngles;
            return $"{pos.x},{pos.y},{pos.z},{rot.x},{rot.y},{rot.z}";
        }
        public static string Unravel(this Vector3[] vec3Arr)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < vec3Arr.Length; i++)
            {
                var vec3Item = vec3Arr[i];
                sb.Append($"({vec3Item.x},{vec3Item.y},{vec3Item.z})");
                if (i < vec3Arr.Length - 1)
                    sb.Append(",");
            }
            return sb.ToString();
        }
        public static void SaveCameraParameters(Camera cam, ARCameraManager arCameraManager, string dataSaveFilePath)
        {
            ExtendedCameraParameters parameters = cam.ExtractConfigsFromCamera(arCameraManager);
            string json = JsonUtility.ToJson(parameters, true);
            File.WriteAllText(dataSaveFilePath, json);
        }
        public static void SaveCameraParameters(Camera cam, string dataSaveFilePath)
        {
            ExtendedCameraParameters parameters = cam.ExtractConfigsFromCamera();
            string json = JsonUtility.ToJson(parameters, true);
            File.WriteAllText(dataSaveFilePath, json);
        }

        public static bool LoadCameraParameters(Camera cam, string dataSaveFilePath)
        {
            if (File.Exists(dataSaveFilePath))
            {
                string json = File.ReadAllText(dataSaveFilePath);
                ExtendedCameraParameters parameters = JsonUtility.FromJson<ExtendedCameraParameters>(json);
                cam.ApplyConfigsToCamera(parameters);
                return true;
            }
            else
            {
                Debug.LogWarning($"File not found: {dataSaveFilePath}");
                return false;
            }
        }
        public static ExtendedCameraParameters ExtractConfigsFromCamera(this Camera camera, ARCameraManager arCameraManager)
        {
            // Get actual intrinsic parameters from the ARCameraManager
            Matrix4x4 intrinsicMatrix = Matrix4x4.identity;
            float focalLengthX = 0f;
            float focalLengthY = 0f;
            float principalPointX = 0f;
            float principalPointY = 0f;
            Vector2Int screenRes = Vector2Int.zero;

            if (arCameraManager.TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
            {
                screenRes = cameraIntrinsics.resolution;
                focalLengthX = cameraIntrinsics.focalLength.x;
                focalLengthY = cameraIntrinsics.focalLength.y;
                principalPointX = cameraIntrinsics.principalPoint.x;
                principalPointY = cameraIntrinsics.principalPoint.y;

                intrinsicMatrix[0, 0] = focalLengthX;
                intrinsicMatrix[1, 1] = focalLengthY;
                intrinsicMatrix[0, 2] = principalPointX;
                intrinsicMatrix[1, 2] = principalPointY;
                intrinsicMatrix[2, 2] = 1;
                intrinsicMatrix[3, 3] = 1;
            }
            else
            {
                Debug.LogWarning("Failed to get camera intrinsics. Falling back to estimated intrinsics.");
                screenRes.Set(Screen.width, Screen.height);
                intrinsicMatrix = GetIntrinsicMatrix(camera);
                focalLengthX = intrinsicMatrix[0, 0];
                focalLengthY = intrinsicMatrix[1, 1];
                principalPointX = intrinsicMatrix[0, 2];
                principalPointY = intrinsicMatrix[1, 2];
            }

            return new ExtendedCameraParameters
            {
                screenResolution = screenRes,
                intrinsicMatrix = intrinsicMatrix,
                projectionMatrix = camera.projectionMatrix,
                focalLengthX = focalLengthX,
                focalLengthY = focalLengthY,
                principalPointX = principalPointX,
                principalPointY = principalPointY,

                // Extrinsic parameters
                position = camera.transform.position,
                rotation = camera.transform.rotation,
                scale = camera.transform.localScale,
                viewMatrix = camera.worldToCameraMatrix,

                // Additional Camera Parameters
                fieldOfView = camera.fieldOfView,
                aspectRatio = camera.aspect,
                nearClipPlane = camera.nearClipPlane,
                farClipPlane = camera.farClipPlane,
                isOrthographic = camera.orthographic,
                depth = camera.depth,

                clearFlags = camera.clearFlags,
                cullingMask = camera.cullingMask,
                viewportRect = camera.rect
            };
        }

        public static ExtendedCameraParameters ExtractConfigsFromCamera(this Camera camera)
        {
            var intrinsic = GetIntrinsicMatrix(camera);
            return new ExtendedCameraParameters
            {
                intrinsicMatrix = intrinsic,
                projectionMatrix = camera.projectionMatrix,
                focalLengthX = intrinsic[0, 0],
                focalLengthY = intrinsic[1, 1],
                principalPointX = intrinsic[0, 2],
                principalPointY = intrinsic[1, 2],

                // Extrinsic parameters
                position = camera.transform.position,
                rotation = camera.transform.rotation,
                scale = camera.transform.localScale,
                viewMatrix = camera.worldToCameraMatrix,

                // Additional Camera Parameters
                screenResolution = new Vector2Int(Screen.width, Screen.height),
                fieldOfView = camera.fieldOfView,
                aspectRatio = camera.aspect,
                nearClipPlane = camera.nearClipPlane,
                farClipPlane = camera.farClipPlane,
                isOrthographic = camera.orthographic,
                depth = camera.depth,

                clearFlags = camera.clearFlags,
                cullingMask = camera.cullingMask,
                viewportRect = camera.rect
            };
        }

        public static void ApplyConfigsToCamera(this Camera camera, ExtendedCameraParameters camParams)
        {
            // Extrinsic parameters
            camera.transform.position = camParams.position;
            camera.transform.rotation = camParams.rotation;
            camera.transform.localScale = camParams.scale;

            // Intrinsic parameters and projection matrix
            camera.projectionMatrix = camParams.projectionMatrix;

            // Additional Camera Parameters
            camera.fieldOfView = camParams.fieldOfView;
            camera.aspect = camParams.aspectRatio;
            camera.nearClipPlane = camParams.nearClipPlane;
            camera.farClipPlane = camParams.farClipPlane;
            camera.orthographic = camParams.isOrthographic;
            camera.depth = camParams.depth;

            camera.clearFlags = camParams.clearFlags;
            camera.cullingMask = camParams.cullingMask;
            camera.rect = camParams.viewportRect;
        }

        public static Matrix4x4 GetIntrinsicMatrix(Camera camera)
        {
            float sensorWidth = camera.sensorSize.x;
            float sensorHeight = camera.sensorSize.y;
            float focalLength = camera.focalLength;

            float f_x = (focalLength / sensorWidth) * Screen.width;
            float f_y = (focalLength / sensorHeight) * Screen.height;

            float c_x = Screen.width * 0.5f;
            float c_y = Screen.height * 0.5f;

            Matrix4x4 K = new Matrix4x4();
            K[0, 0] = f_x;
            K[1, 1] = f_y;
            K[0, 2] = c_x;
            K[1, 2] = c_y;
            K[2, 2] = 1;
            K[3, 3] = 1;

            return K;
        }


        public static void SaveGameObjectToGLB(GameObject targetGobj, string dataSaveDirPath, string fileName)
        {
            var dataSaveFilePath = Path.Combine(dataSaveDirPath, $"{fileName}.glb");
            SaveGLB(targetGobj, dataSaveFilePath);
        }
        private static async void SaveGLB(GameObject targetGobj, string dataSaveFilePath)
        {
            var exportSettings = new ExportSettings
            {
                Format = GltfFormat.Binary,
                FileConflictResolution = FileConflictResolution.Overwrite,
            };

            var gobjExportSettings = new GameObjectExportSettings
            {
                OnlyActiveInHierarchy = false, //MUST! (by default the inactive gobjs are excluded, otherwise)
                DisabledComponents = true //MUST!
            };

            var export = new GameObjectExport(exportSettings, gobjExportSettings);
            var targetSceneGobjs = new GameObject[] { targetGobj };
            export.AddScene(targetSceneGobjs);

            var success = false;
            success = await export.SaveToFileAndDispose(dataSaveFilePath);
            Assert.IsTrue(success, "glTF Data save attempt failed");
            Debug.Log($"Saved {targetGobj.name} to glb");
        }
        public static void MsgPrint(object content)
        {
            Debug.Log($"<b><color=green>[EXRLogger]</color></b> {content}");
        }
        public static void MsgPrintWarning(object content)
        {
            Debug.LogWarning($"<b><color=yellow>[EXRLogger]</color></b> {content}");
        }
        public static void MsgPrintError(object content)
        {
            Debug.LogError($"<b><color=red>[EXRLogger]</color></b> {content}");
        }
    }

    [Serializable]
    public class ExtendedCameraParameters
    {
        // Intrinsic parameters
        public Matrix4x4 intrinsicMatrix;
        public Matrix4x4 projectionMatrix;
        public float focalLengthX;
        public float focalLengthY;
        public float principalPointX;
        public float principalPointY;

        // Extrinsic parameters
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Matrix4x4 viewMatrix;

        // Additional Camera Parameters
        public Vector2Int screenResolution;
        public float fieldOfView;
        public float aspectRatio;
        public float nearClipPlane;
        public float farClipPlane;
        public bool isOrthographic;
        public float depth;
        public CameraClearFlags clearFlags;
        public int cullingMask;
        public Rect viewportRect;
    }
}