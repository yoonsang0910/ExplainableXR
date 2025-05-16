using GLTFast.Export;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

namespace ExplainableXR
{
    using static ExplainableXR.Utility;
    public class ObjectRecordManager
    {
        private string dirName = null;
        private int objectIndex = 1;
        private Dictionary<GameObject, string> gobjToGLBFileName = new();
        private ExportSettings exportSettings = new ExportSettings
        {
            Format = GltfFormat.Binary,
            FileConflictResolution = FileConflictResolution.Overwrite,
        };
        private GameObjectExportSettings gobjExportSettings = new GameObjectExportSettings
        {
            OnlyActiveInHierarchy = false, //MUST! (by default the inactive gobjs are excluded, otherwise)
            DisabledComponents = true //MUST!
        };
        public ObjectRecordManager(string rootDirName)
        {
            dirName = Path.Combine(rootDirName, "Object");
            Directory.CreateDirectory(dirName);
        }
        public string SaveObject(GameObject targetGobj)
        {
            //Reuse glb resources (no lag)
            if (gobjToGLBFileName.ContainsKey(targetGobj))
                return gobjToGLBFileName[targetGobj];

            var fileName = $"{objectIndex++}.glb";
            var dataSaveFilePath = Path.Combine(dirName, fileName);
            SaveGLB(targetGobj, dataSaveFilePath);
            gobjToGLBFileName[targetGobj] = fileName;
            return fileName;
        }
        private async void SaveGLB(GameObject targetGobj, string dataSaveFilePath)
        {
            var export = new GameObjectExport(exportSettings, gobjExportSettings);
            var targetSceneGobjs = new GameObject[] { targetGobj };
            export.AddScene(targetSceneGobjs);

            var success = false;
            success = await export.SaveToFileAndDispose(dataSaveFilePath);
            Assert.IsTrue(success, "glTF Data save attempt failed");
            MsgPrint($"Referent GLB file saved at :{dataSaveFilePath}");
        }
    }
}