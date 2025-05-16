using UnityEditor;
using UnityEngine;
using System.IO;
using System.Drawing.Printing;

public class TemplateBasedLogDefinitionCreator
{
    [MenuItem("Assets/Create/ExplainableXR/Template-based Logging Definition", priority = 1)]
    public static void CreateTemplateBasedLogDefiniton()
    {
        TemplateBasedLogDefinition asset = ScriptableObject.CreateInstance<TemplateBasedLogDefinition>();

        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(path))
        {
            path = "Assets";
        }
        else if (Path.GetExtension(path) != "")
        {
            path = path.Replace(Path.GetFileName(path), "");
        }

        string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/TemplateBasedLogDefinition.asset");

        AssetDatabase.CreateAsset(asset, assetPathAndName);
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;
    }
}
