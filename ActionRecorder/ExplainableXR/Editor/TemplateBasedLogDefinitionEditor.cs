using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Reflection;
using System.CodeDom;
using System.CodeDom.Compiler;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.Controls;
using Unity.XR.CoreUtils;
using System.Linq;
using Unity.VisualScripting;

namespace ExplainableXR
{
    public class TemplateBasedLogDefinitionEditor : EditorWindow
    {
        private TemplateBasedLogDefinition templateBasedLogDefinition;
        private Vector2 scrollPos;

        private Action selectedAction;
        private Intent selectedIntent;

        private string newActionName = "";
        private string newIntentName = "";
        private string newIntentType = "";
        private string newBindingPath = "";

        private IEnumerable<string> actionTriggerSourceData = null;
        private string[] actionTriggerSources = null;
        private Dictionary<string, string> actionTriggerSourceToRealNameDict = new();
        private string[] actionTriggerSourcesBindings = null;
        private string curActionTriggerSource = null;
        private InputActionType curActionTriggerSourceActionType = default;
        private string[] actionTriggerSourceControlTypes = null;

        private void OnEnable()
        {
            LoadTriggerSourceLayouts();
        }

        private void LoadTriggerSourceLayouts()
        {
            actionTriggerSourceData = InputSystem.ListLayouts();
            var devices = new List<string>();
            foreach (var layout in actionTriggerSourceData)
            {
                var layoutName = new InternedString(layout);
                var layoutData = InputSystem.LoadLayout(layoutName);
                var displayName = layoutData.displayName;
                if (layoutData.isDeviceLayout)
                {
                    devices.Add(displayName);
                    actionTriggerSourceToRealNameDict[displayName] = layoutData.name;
                }
            }
            devices.Sort();
            actionTriggerSources = devices.ToArray();
        }

        private string[] UpdateBindings(string deviceDisplayName, InputActionType actionType)
        {
            if (curActionTriggerSource == deviceDisplayName && curActionTriggerSourceActionType == actionType)
                return actionTriggerSourcesBindings;

            var bindings = new List<string>();
            foreach (var layout in actionTriggerSourceData)
            {
                var layoutName = new InternedString(layout);
                var layoutData = InputSystem.LoadLayout(layoutName);
                var displayName = layoutData.displayName;
                if (layoutData.isDeviceLayout && displayName == deviceDisplayName)
                {
                    var controlItems = layoutData.controls;
                    foreach (var controlItem in controlItems)
                    {
                        if (actionType == InputActionType.Button)
                        {
                            // Debug.Log($"{controlItem.name} |{controlItem.layout}|");
                            if (string.IsNullOrEmpty(controlItem.layout))
                                continue;

                            var controlLayout = InputSystem.LoadLayout(controlItem.layout);
                            if (controlLayout == null || !typeof(ButtonControl).IsAssignableFrom(controlLayout.type))
                                continue;

                        }
                        bindings.Add(controlItem.name);
                    }
                    break;
                }
            }
            bindings.Sort();
            actionTriggerSourcesBindings = bindings.ToArray();
            curActionTriggerSource = deviceDisplayName;
            curActionTriggerSourceActionType = actionType;

            return actionTriggerSourcesBindings;
        }


        // [MenuItem("Custom/XR Log Definition Editor")]
        public static void ShowWindow()
        {
            GetWindow<TemplateBasedLogDefinitionEditor>("XR Log Definition Editor");
        }

        private void OnGUI()
        {
            if (templateBasedLogDefinition == null)
            {
                EditorGUILayout.LabelField("Select an XR Log Definition file to edit.");
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.BeginHorizontal();

            if (selectedAction == null && templateBasedLogDefinition.Actions.Count > 0)
            {
                selectedAction = templateBasedLogDefinition.Actions[0];
                newActionName = selectedAction.Name;
            }

            // Action
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("User Actions", EditorStyles.boldLabel);
            if (GUILayout.Button("+", ButtonCustomStyles.IconButton, GUILayout.Width(20)))
            {
                templateBasedLogDefinition.Actions.Add(new Action { Name = $"New Action{templateBasedLogDefinition.Actions.Count + 1}", Intents = new List<Intent>() });
                SaveAssetChanges();
            }
            if (GUILayout.Button("-", ButtonCustomStyles.IconButton, GUILayout.Width(20)))
            {
                if (templateBasedLogDefinition.Actions.Count > 0)
                {
                    if (selectedAction == null)
                        selectedAction = templateBasedLogDefinition.Actions[templateBasedLogDefinition.Actions.Count - 1];

                    var index = templateBasedLogDefinition.Actions.IndexOf(selectedAction);
                    templateBasedLogDefinition.Actions.Remove(selectedAction);
                    selectedAction = null;
                    selectedIntent = null;
                    newActionName = "";
                    newIntentName = "";
                    newIntentType = "";

                    // Automatically selecting the closest item
                    if (templateBasedLogDefinition.Actions.Count > 0)
                    {
                        if (index >= templateBasedLogDefinition.Actions.Count)
                            index = templateBasedLogDefinition.Actions.Count - 1;
                        selectedAction = templateBasedLogDefinition.Actions[index];
                    }
                    newActionName = selectedAction.Name;
                    SaveAssetChanges();
                }
            }
            // DrawHorizontalLine();
            EditorGUILayout.EndHorizontal();

            foreach (var action in templateBasedLogDefinition.Actions)
            {
                GUIStyle style = selectedAction == action ? ButtonCustomStyles.SelectedButton : ButtonCustomStyles.FlatButton;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"[{action.ActionTriggerSource}] {action.Name}", style))
                {
                    selectedAction = action;
                    selectedIntent = null;
                    newActionName = action.Name;
                    newIntentName = "";
                    newIntentType = "";
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            DrawVerticalLine();
            GUILayout.Space(2);

            // Intents
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            if (selectedAction != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Action Intents", EditorStyles.boldLabel);
                if (GUILayout.Button("+", ButtonCustomStyles.IconButton, GUILayout.Width(20)))
                {
                    selectedAction.Intents.Add(new Intent { Name = $"New Intent{selectedAction.Intents.Count + 1}" });
                    SaveAssetChanges();
                }
                if (GUILayout.Button("-", ButtonCustomStyles.IconButton, GUILayout.Width(20)))
                {
                    if (selectedAction.Intents.Count > 0)
                    {
                        if (selectedIntent == null)
                            selectedIntent = selectedAction.Intents[selectedAction.Intents.Count - 1];

                        var index = selectedAction.Intents.IndexOf(selectedIntent);
                        selectedAction.Intents.Remove(selectedIntent);
                        selectedIntent = null;
                        newIntentName = "";
                        newIntentType = "";

                        // Automatically select the closest item
                        if (selectedAction.Intents.Count > 0)
                        {
                            if (index >= selectedAction.Intents.Count)
                                index = selectedAction.Intents.Count - 1;
                            selectedIntent = selectedAction.Intents[index];
                            newIntentName = selectedIntent.Name;
                            newIntentType = selectedIntent.IntentType;
                        }
                        SaveAssetChanges();
                    }
                }
                // DrawHorizontalLine();
                EditorGUILayout.EndHorizontal();

                foreach (var intent in selectedAction.Intents)
                {
                    GUIStyle style = selectedIntent == intent ? ButtonCustomStyles.SelectedButton : ButtonCustomStyles.FlatButton;
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(intent.Name, style))
                    {
                        selectedIntent = intent;
                        newIntentName = intent.Name;
                        newIntentType = intent.IntentType;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();

            DrawVerticalLine();
            GUILayout.Space(2);

            // Action and Intent Properties
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            if (selectedAction != null && selectedIntent == null)
            {
                EditorGUILayout.LabelField("User Action Properties", EditorStyles.boldLabel);
                DrawHorizontalLine();
                GUILayout.Space(1);
                newActionName = EditorGUILayout.TextField("Action Name", newActionName, GUILayout.Width(265));

                var selectedItemIndex = Mathf.Max(Array.IndexOf(actionTriggerSources, selectedAction.ActionTriggerSource), 0);
                var newSelectedItemIndex = EditorGUILayout.Popup("Action Trigger Source", selectedItemIndex, actionTriggerSources, GUILayout.Width(265));
                selectedAction.ActionTriggerSource = actionTriggerSources[newSelectedItemIndex];

                selectedAction.Name = newActionName;

                GUILayout.Space(20);
                EditorGUILayout.LabelField("Action Trigger Source Bindings", EditorStyles.boldLabel);
                DrawHorizontalLine();

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("Trigger Type", GUILayout.Width(80));
                GUILayout.Space(40);
                EditorGUILayout.LabelField("Binding Path", GUILayout.Width(110));
                EditorGUILayout.LabelField("Remove", GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(2);

                foreach (var binding in selectedAction.Bindings)
                {
                    EditorGUILayout.BeginHorizontal();
                    binding.ActionType = (InputActionType)EditorGUILayout.EnumPopup("", binding.ActionType, GUILayout.Width(80));
                    var bindsList = UpdateBindings(selectedAction.ActionTriggerSource, binding.ActionType);
                    // Only allow input when there is a support (binding counts>0)
                    if (bindsList.Length <= 0)
                    {
                        GUI.enabled = false;
                        EditorGUILayout.EndHorizontal();
                        continue;
                    }
                    else
                        GUI.enabled = true;

                    var selectedBindingItemIndex = Mathf.Max(Array.IndexOf(bindsList, binding.ActionTriggerSourceBinding), 0);
                    var newSelectedBindingItemIndex = EditorGUILayout.Popup("", selectedBindingItemIndex, bindsList, GUILayout.Width(150));
                    binding.ActionTriggerSourceBinding = bindsList[newSelectedBindingItemIndex];
                    GUILayout.Space(10);
                    if (GUILayout.Button("-", GUILayout.Width(30)))
                    {
                        selectedAction.Bindings.Remove(binding);
                        SaveAssetChanges();
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                // GUI.enabled = true;
                if (GUILayout.Button("Add Binding", GUILayout.Width(100)))
                {
                    selectedAction.Bindings.Add(new Binding());
                }
                SaveAssetChanges();
            }
            else if (selectedIntent != null)
            {
                EditorGUILayout.LabelField("Action Intent Properties", EditorStyles.boldLabel);
                DrawHorizontalLine();
                GUILayout.Space(2);
                newIntentName = EditorGUILayout.TextField("Intent Name", newIntentName);
                // newIntentType = EditorGUILayout.TextField("Intent Type", newIntentType);

                selectedIntent.Name = newIntentName;
                selectedIntent.IntentType = newIntentType;
                SaveAssetChanges();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Generate C# XR Action Logger", GUILayout.Height(25), GUILayout.ExpandWidth(true)))
            {
                GenerateXRActionLoggerClass();
            }
        }

        private void DrawVerticalLine()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(1), GUILayout.ExpandHeight(true));
            rect.width = 1;
            EditorGUI.DrawRect(rect, Color.gray);
        }

        private void DrawHorizontalLine()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, GUILayout.Height(1), GUILayout.ExpandWidth(true));
            rect.height = 1;
            EditorGUI.DrawRect(rect, Color.gray);
        }

        private void SaveAssetChanges()
        {
            EditorUtility.SetDirty(templateBasedLogDefinition);
            AssetDatabase.SaveAssets();
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceID) as TemplateBasedLogDefinition;
            if (obj != null)
            {
                var window = GetWindow<TemplateBasedLogDefinitionEditor>();
                window.templateBasedLogDefinition = obj;
                return true;
            }
            return false;
        }

        private void GenerateXRActionLoggerClass()
        {
            string className = "TemplateBasedLogging";
            string filePath = Application.dataPath + "/ExplainableXR/" + className + ".cs";

            if (!Directory.Exists(Application.dataPath + "/ExplainableXR"))
                Directory.CreateDirectory(Application.dataPath + "/ExplainableXR");

            int fileIndex = 2;
            while (File.Exists(filePath))
                filePath = Application.dataPath + "/ExplainableXR/" + className + $"_{fileIndex++}.cs";

            if (fileIndex >= 3)
                className = $"{className}_{fileIndex-1}";

            CodeCompileUnit compileUnit = new CodeCompileUnit();
            CodeNamespace codeNamespace = new CodeNamespace("ExplainableXR");
            codeNamespace.Imports.Add(new CodeNamespaceImport("UnityEngine"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("Unity.XR.CoreUtils"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("UnityEngine.InputSystem"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("UnityEngine.InputSystem.XR"));

            CodeTypeDeclaration generatedClass = new CodeTypeDeclaration(className)
            {
                IsClass = true,
                TypeAttributes = TypeAttributes.Public
            };
            generatedClass.BaseTypes.Add(new CodeTypeReference("MonoBehaviour"));


            // Data initialization for clean code
            var userActionDataList = new List<UserActionData>();
            foreach (var userAction in templateBasedLogDefinition.Actions)
                userActionDataList.Add(new UserActionData(userAction, actionTriggerSourceToRealNameDict));


            //Member varible generation
            foreach (var userActionData in userActionDataList)
            {
                foreach (var bindingData in userActionData.BindingDataList)
                {
                    CodeMemberField privateField = new CodeMemberField
                    {
                        Name = bindingData.BindingMemberFieldName,
                        Type = new CodeTypeReference(typeof(InputAction)),
                        Attributes = MemberAttributes.Private
                    };
                    generatedClass.Members.Add(privateField);
                }
            }


            //Add Awake function
            CodeMemberMethod awakeMethod = new CodeMemberMethod
            {
                Name = "Awake",
                Attributes = MemberAttributes.Private
            };

            foreach (var userActionData in userActionDataList)
            {
                foreach (var binding in userActionData.BindingDataList)
                {
                    var constructor = new CodeObjectCreateExpression(
                        "InputAction",
                        new CodePrimitiveExpression($"{binding.InputActionName.FirstCharacterToUpper()}"), // name
                        new CodeFieldReferenceExpression(new CodeTypeReferenceExpression("InputActionType"), binding.BindingActionType.ToString()), // type
                        new CodePrimitiveExpression(binding.BindingFullPath) // binding
                        );
                    awakeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression(binding.BindingMemberFieldName), constructor));
                }
            }
            generatedClass.Members.Add(awakeMethod);

            //Add Logging condition function generation
            foreach (var userActionData in userActionDataList)
            {
                foreach (var callbackFuncName in userActionData.InputActionCallbackMethodNameList.Keys)
                {
                    CodeMemberMethod method = new CodeMemberMethod
                    {
                        Name = $"{callbackFuncName}_Logging_Condition",
                        Attributes = MemberAttributes.Private,
                        ReturnType = new CodeTypeReference(typeof(bool))
                    };

                    method.Parameters.Add(new CodeParameterDeclarationExpression(
                         "InputAction.CallbackContext", "context"));

                    var valueVariable = new CodeVariableDeclarationStatement(
                            typeof(object), "value", new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("context"), "ReadValueAsObject"));
                    method.Statements.Add(valueVariable);

                    var valueTypeVariable = new CodeVariableDeclarationStatement(
                        typeof(string), "valueType", new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("Utility"), "TypeToStringName",
                        new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("value"), "GetType")));
                    method.Statements.Add(valueTypeVariable);

                    var logExpression = new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("Debug"),
                    "Log", new CodeSnippetExpression($"$\"Action : {{context.action.name}}, Value : {{value}}, Typeof({{valueType}})\""));
                    method.Statements.Add(logExpression);

                    method.Statements.Add(new CodeSnippetStatement("\n            // Insert logging condition logic here..."));
                    var ifCondition = new CodeConditionStatement(
                                new CodeSnippetExpression("true"),
                                new CodeStatement[] { new CodeMethodReturnStatement(new CodePrimitiveExpression(true)) });
                    method.Statements.Add(ifCondition);

                    var finalReturnFalse = new CodeMethodReturnStatement(new CodePrimitiveExpression(false));
                    method.Statements.Add(finalReturnFalse);

                    generatedClass.Members.Add(method);
                }
            }

            // Add Log function
            CodeMemberMethod logMethod = new CodeMemberMethod
            {
                Name = "LogXRUserData",
                Attributes = MemberAttributes.Private
            };
            logMethod.Parameters.Add(new CodeParameterDeclarationExpression("InputAction.CallbackContext", "context"));
            logMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof(string), "userAction"));
            logMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof(string), "userActionIntent"));
            logMethod.Statements.Add(new CodeSnippetStatement("            // Insert log data logic here...\n"));

            var logExpression2 = new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("Debug"),
            "Log", new CodeSnippetExpression($"$\"[Logging XR User Data] User Action : {{userAction}}, Intent : {{userActionIntent}})\""));
            logMethod.Statements.Add(logExpression2);
            generatedClass.Members.Add(logMethod);


            //Add User Action Trigger function generation
            foreach (var userActionData in userActionDataList)
            {
                foreach (var callbackFuncName in userActionData.InputActionCallbackMethodNameList.Keys)
                {
                    var intentName = userActionData.InputActionCallbackMethodNameList[callbackFuncName];
                    CodeMemberMethod method = new CodeMemberMethod
                    {
                        Name = callbackFuncName,
                        Attributes = MemberAttributes.Private
                    };

                    method.Parameters.Add(new CodeParameterDeclarationExpression(
                         "InputAction.CallbackContext", "context"));

                    var conditionInvoke = new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(null, $"{callbackFuncName}_Logging_Condition"),
                        new CodeVariableReferenceExpression("context"));

                    var logInvoke = new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(null, "LogXRUserData"), new CodeVariableReferenceExpression("context"),
                        new CodeVariableReferenceExpression($"\"{userActionData.Action.Name}\""), new CodeVariableReferenceExpression($"\"{intentName}\""));

                    // Create the if statement
                    var ifCondition = new CodeConditionStatement(
                        conditionInvoke,
                        new CodeExpressionStatement(logInvoke));

                    method.Statements.Add(ifCondition);
                    generatedClass.Members.Add(method);
                }
            }


            // Add OnEnable function
            CodeMemberMethod onEnableMethod = new CodeMemberMethod
            {
                Name = "OnEnable",
                Attributes = MemberAttributes.Private
            };
            foreach (var userActionData in userActionDataList)
            {
                foreach (var binding in userActionData.BindingDataList)
                {
                    onEnableMethod.Statements.Add(new CodeMethodInvokeExpression(
                        new CodeVariableReferenceExpression(binding.BindingMemberFieldName),
                        "Enable"));

                    foreach (var callbackFuncName in userActionData.InputActionCallbackMethodNameList.Keys)
                    {
                        onEnableMethod.Statements.Add(new CodeAttachEventStatement(
                            new CodeEventReferenceExpression(new CodeVariableReferenceExpression(binding.BindingMemberFieldName), "performed"),
                            new CodeMethodReferenceExpression(null, callbackFuncName)));
                    }
                }
            }
            generatedClass.Members.Add(onEnableMethod);


            // Add OnDisable function
            CodeMemberMethod onDisableMethod = new CodeMemberMethod
            {
                Name = "OnDisable",
                Attributes = MemberAttributes.Private
            };
            foreach (var userActionData in userActionDataList)
            {
                foreach (var binding in userActionData.BindingDataList)
                {
                    onDisableMethod.Statements.Add(new CodeMethodInvokeExpression(
                        new CodeVariableReferenceExpression(binding.BindingMemberFieldName),
                        "Disable"));

                    foreach (var callbackFuncName in userActionData.InputActionCallbackMethodNameList.Keys)
                    {
                        onDisableMethod.Statements.Add(new CodeRemoveEventStatement(
                            new CodeEventReferenceExpression(new CodeVariableReferenceExpression(binding.BindingMemberFieldName), "performed"),
                            new CodeMethodReferenceExpression(null, callbackFuncName)));
                    }
                }
            }
            generatedClass.Members.Add(onDisableMethod);



            //Add the rest of default functions
            CodeMemberMethod startMethod = new CodeMemberMethod
            {
                Name = "Start",
                Attributes = MemberAttributes.Private
            };
            startMethod.Statements.Add(new CodeSnippetStatement("        // Start logic here"));
            generatedClass.Members.Add(startMethod);

            CodeMemberMethod updateMethod = new CodeMemberMethod
            {
                Name = "Update",
                Attributes = MemberAttributes.Private
            };
            updateMethod.Statements.Add(new CodeSnippetStatement("        // Update logic here"));
            generatedClass.Members.Add(updateMethod);



            codeNamespace.Types.Add(generatedClass);
            compileUnit.Namespaces.Add(codeNamespace);

            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");
                CodeGeneratorOptions options = new CodeGeneratorOptions
                {
                    BracingStyle = "C",
                    IndentString = "    "
                };
                provider.GenerateCodeFromCompileUnit(compileUnit, writer, options);
            }

            AssetDatabase.Refresh();
            Debug.Log($"C# Interaction logger generated successfully at {filePath}");
        }
    }

    //One class instance for each UserAction (may involve 1 or more bindings)
    public class UserActionData
    {
        public Action Action;
        public class BindingData
        {
            public string InputActionName; //CenterEyePositionAction (Input Action name)
            public string BindingMemberFieldName; //m_centerEyePositionAction (Member variable name)
            public string BindingName; //centerEyePositionAction
            public InputActionType BindingActionType; //Value, Passthrough
            public string BindingReadValueType; //Vector3
            public string BindingFullPath; //<XRHMD>/centerEyePosition 
        }

        public List<BindingData> BindingDataList = new();
        public Dictionary<string, string> InputActionCallbackMethodNameList = new(); //<OnMove(), IntentName>
        public UserActionData(Action userAction, Dictionary<string, string> actionTriggerSourceToRealName)
        {
            Action = userAction;
            InitBindingData(userAction, actionTriggerSourceToRealName);
            InitCallbackMethodNames(userAction);
        }
        private void InitBindingData(Action userAction, Dictionary<string, string> actionTriggerSourceToRealName)
        {
            foreach (var binding in userAction.Bindings)
            {
                var bindingPath = binding.ActionTriggerSourceBinding;
                var bindingSourceDeviceDisplayName = userAction.ActionTriggerSource;
                var bindingSourceDeviceRealName = actionTriggerSourceToRealName[userAction.ActionTriggerSource];
                var controlType = GetControlTypeFromBinding(bindingSourceDeviceDisplayName, bindingPath);

                var fullBindingPath = $"<{bindingSourceDeviceRealName}>/{bindingPath}";
                var bindingMemberFieldName = RemoveWhitespaces($"{userAction.ActionTriggerSource.ToLower().FirstCharacterToUpper()}_" +
                    $"{userAction.Name.ToLower().FirstCharacterToUpper()}_" +
                    $"{binding.ActionTriggerSourceBinding.ToLower().FirstCharacterToUpper()}");

                var bindingData = new BindingData()
                {
                    InputActionName = bindingMemberFieldName,
                    BindingMemberFieldName = $"m_{bindingMemberFieldName}",
                    BindingName = binding.ActionTriggerSourceBinding,
                    BindingActionType = binding.ActionType,
                    BindingReadValueType = controlType,
                    BindingFullPath = fullBindingPath
                };
                BindingDataList.Add(bindingData);
            }
        }
        private void InitCallbackMethodNames(Action userAction)
        {
            var actionName = userAction.Name;
            if (userAction.Intents.Count <= 0)
            {
                InputActionCallbackMethodNameList[AutoGenerateMethodName(actionName)] = "";
                return;
            }
            foreach (var userIntent in userAction.Intents)
            {
                var intentName = userIntent.Name;
                InputActionCallbackMethodNameList[AutoGenerateMethodName(actionName, intentName)] = intentName;
            }
        }

        private string RemoveWhitespaces(string source)
        {
            return new string(source.Where(c => !char.IsWhiteSpace(c)).ToArray());
        }
        private string AutoGenerateMethodName(string originalActionName, string originalIntentName = null)
        {
            if (originalIntentName == null)
                return $"On{RemoveWhitespaces(originalActionName).ToLower().FirstToUpper()}";
            else
                return $"On{RemoveWhitespaces(originalActionName).ToLower().FirstToUpper()}_{RemoveWhitespaces(originalIntentName).ToLower().FirstToUpper()}";
        }
        private string GetControlTypeFromBinding(string deviceDisplayName, string bindingPath)
        {
            foreach (var layout in InputSystem.ListLayouts())
            {
                var layoutName = new InternedString(layout);
                var layoutData = InputSystem.LoadLayout(layoutName);
                var displayName = layoutData.displayName;
                if (layoutData.isDeviceLayout && displayName == deviceDisplayName)
                {
                    var controlItems = layoutData.controls;
                    foreach (var controlItem in controlItems)
                    {
                        if (string.IsNullOrEmpty(controlItem.layout))
                            continue;

                        if (controlItem.name == bindingPath)
                        {
                            var controlLayout = InputSystem.LoadLayout(controlItem.layout);
                            return Utility.TypeToStringName(controlLayout.GetValueType());
                        }
                    }
                }
            }
            return null;
        }
    }
}