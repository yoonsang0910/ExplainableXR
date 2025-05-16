using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using Unity.VisualScripting;
using System.Linq;
using UnityEngine.XR.ARFoundation;
using Whisper.Utils;

namespace ExplainableXR
{
    using static ExplainableXR.Utility;
    public enum ActionType // Primarily used for post-processing
    {
        Discrete, // Actions that can be completed instantly (e.g. UI Button Press, Finger pinch)
        Continuous // Actions with continuous behavior (e.g. Moving object, Gazing at an object, Speech/Audio)
    }
    public enum ReferentType
    {
        None,
        Virtual, // Referent in the Virtual Reality (e.g. Unity cube object)
        Physical // Referent in the Phyiscal Reality (e.g. Physical object in the real-world)
    }
    public enum ActionContextType
    {
        None,
        Virtual, // Virtual context (Unity camera's snapshot/pointcloud)
        Physical // Real world context (Physical camera's snapshot/pointcloud; From an AR device camera sensor)
    }

    //Predefined trigger sources (Users can custom add them)
    public enum ActionTriggerSource
    {
        XRHMD,
        XRController_L,
        XRController_R,
        XRHand_L,
        XRHand_R,
        ARHandHeld,
        Microphone
    }


    // UAD Format : (User Action-oriented Descriptor)
    [Serializable]
    public struct UserActionDescriptor
    {
        public int ActionTypeAndIdBit; // Discrete (==0)vs Continuous Action (>0; Continuous Action ID number)
        public string User; //Who : ("User1", "User2", Device-UID (default))
        public string Location; //Where : ("pos.x, pos.y, pos.z, rot.euler.x, rot.euler.y, rot.euler.z")
        public string Timestamp; //When : Start ("yyMMdd_HHmmss_fff")
        public float Duration; //When : Action length (End-Start)
        public string UserAction; //What : User Action ("Navigate", "MoveObject", "LookAt", "DirectPinch", "IndirectPinch", "Touch", "CastRay", "Speak")
        public string UserIntent; //Why : Action intent ("UI select", "Data visualization", "Unknown"(default))
        public string ActionTriggerSource; //How : Action trigger medium ("XR HMD", "XR Controller", "AR HandHeld", "XRHand", "Audio")
        // public string ActionReferentHitPoint; //Target of the interaction initial hit point ("pos.x, pos.y, pos.z")
        public string ActionReferent; // Target of the interaction (FilePath to : rayHitGobj.glb, speech_file.wav, postDefinedARscreenshot.png (inference using LLM))
        public string ActionReferentTransform; // Referent Transform : For glb save time reduction optimization ("pos.x, pos.y, pos.z, rot.euler.x, rot.euler.y, rot.euler.z")
        public string ActionContext; // Action contextual data (screenshot of img, depth) for VR (Virtual cam), AR (Virtual cam or Physical cam), MR (Virtual cam or Physical cam)
        public string ActionReferentName; // Referent name (Virtual entity : Gameobject name; Physical entity : object class)
        public string ActionReferentType;
        public string ActionContextType;
    }
    // + Separate ActionTriggerSource glb for the interface : ("XR HMD", "XR Controller", "AR HandHeld", "XRHand", "Microphone")

    /*
    <Example usecase scenario of the 'UAD' format>
    At the beginning of the mixed reality user study session ({When}), 
    Subject1 ({Who}) pressed {What} a UI button {ActionReferent} that is anchored near the starting position ({Where}), 
    with his hand's pinch gesture ({How}), to visualize immersive analytics data ({Why}). 
    */
    public class Logger
    {
        private static Logger instance = null;
        private static readonly object lockObject = new object();
        private string dataSavePath = null;
        private List<UserActionDescriptor> logDataList = new List<UserActionDescriptor>();
        private readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.Indented
        };
        private string curTime => DateTime.Now.ToString("yyMMdd_HHmmss_fff");
        private ImageRecordManager imageRecordManager = null;
        private ObjectRecordManager objectManager = null;
        private AudioRecordManager audioRecordManager = null;
        private string userID = "";

        #region Action Recorder Initialization
        //For AR environment
        private Logger(MonoBehaviour monoBehaviour, Camera snapshotCam,
        ARCameraManager arCameraManager, AROcclusionManager occlusionManager,
        MicrophoneRecord microphoneManager, string userID)
        {
            var dataSaveTime = DateTime.Now.ToString("yyMMdd_HHmmss");
            var rootDirName = Path.Combine(Application.persistentDataPath, dataSaveTime);
            Directory.CreateDirectory(rootDirName);
            imageRecordManager = new ImageRecordManager(monoBehaviour, snapshotCam, arCameraManager, occlusionManager, rootDirName);
            objectManager = new ObjectRecordManager(rootDirName);
            audioRecordManager = new AudioRecordManager(microphoneManager, rootDirName);
            dataSavePath = Path.Combine(rootDirName, $"Log_{dataSaveTime}.json");
            this.userID = userID;
            LogUtils.Level = LogLevel.Warning;
        }
        //For VR environment (No passthrough or physical camera involved)
        private Logger(MonoBehaviour monoBehaviour, Camera snapshotCam,
        MicrophoneRecord microphoneManager, string userID)
        {
            var dataSaveTime = DateTime.Now.ToString("yyMMdd_HHmmss");
            var rootDirName = Path.Combine(Application.persistentDataPath, dataSaveTime);
            Directory.CreateDirectory(rootDirName);
            imageRecordManager = new ImageRecordManager(monoBehaviour, snapshotCam, rootDirName);
            objectManager = new ObjectRecordManager(rootDirName);
            audioRecordManager = new AudioRecordManager(microphoneManager, rootDirName);
            dataSavePath = Path.Combine(rootDirName, $"Log_{dataSaveTime}.json");
            this.userID = userID;
            LogUtils.Level = LogLevel.Warning;
        }

        public static Logger Initialize(
            MonoBehaviour monoBehaviour, Camera snapshotCam,
            ARCameraManager arCameraManager, AROcclusionManager occlusionManager,
            MicrophoneRecord microphoneManager = null, string userID = null)
        {
            if (instance == null)
            {
                lock (lockObject)
                {
                    if (userID == null || userID.Trim() == "")
                        userID = GetDeviceUniqueID();
                    instance = new Logger(monoBehaviour, snapshotCam, arCameraManager, occlusionManager, microphoneManager, userID);
                }
            }
            return instance;
        }
        public static Logger Initialize(
            MonoBehaviour monoBehaviour, Camera snapshotCam,
            MicrophoneRecord microphoneManager = null, string userID = null)
        {
            if (instance == null)
            {
                lock (lockObject)
                {
                    if (userID == null || userID.Trim() == "")
                        userID = GetDeviceUniqueID();
                    instance = new Logger(monoBehaviour, snapshotCam, microphoneManager, userID);
                }
            }
            MsgPrint($"Initializing Explainable XR Logger with UserID='{userID}'");
            return instance;
        }
        #endregion

        #region Discrete Action Logging
        //Discrete action (e.g., XR object select, UI pinch)
        public void LogDiscreteAction(
            string userAction,
            string userActionIntent,
            ActionTriggerSource userActionTriggerSrc,
            Transform userActionTriggerSrcTransform,
            object userActionReferent,
            Transform userActionReferentTransform,
            ReferentType userActionReferentType,
            ActionContextType userActionContextType) => LogAction(
                userAction, userActionIntent, (int)ActionType.Discrete, userActionTriggerSrc,
                userActionTriggerSrcTransform, DISCRETE_ACTION_DURATION_SEC,
                userActionReferent, userActionReferentTransform, userActionReferentType,
                userActionContextType);
        #endregion

        #region Continuous Action Logging
        //Continuous action (e.g., XR grab and move, Speech/Audio/Microphone inputs)
        public static int continuousActionLogId = 2; //Unique ID assigned to every LogContinuousActionStart call
        //uint casting is a hassle for the user, just using int instead
        private static Dictionary<int, CachedActionProperties> cachedActions = new();
        private class CachedActionProperties
        {
            public string userAction;
            public string userActionIntent;
            public ActionTriggerSource userActionTriggerSrc;
            public Transform userActionTriggerSrcTransform;
            public object userActionReferent;
            public Transform userActionReferentTransform;
            public ReferentType userActionReferentType;
            public ActionContextType userActionContextType;
        }
        private int GetNewContinuousActionLogId()
        {
            if (continuousActionLogId >= int.MaxValue)
                continuousActionLogId = 2;
            return continuousActionLogId++;
        }
        // First LogContinuousAction() invocation
        public int LogContinuousActionBegin(
            string userAction,
            string userActionIntent,
            ActionTriggerSource userActionTriggerSrc,
            Transform userActionTriggerSrcTransform,
            object userActionReferent,
            Transform userActionReferentTransform,
            ReferentType userActionReferentType,
            ActionContextType userActionContextType)
        {
            int logId = GetNewContinuousActionLogId();
            cachedActions[logId] = new CachedActionProperties()
            {
                userAction = userAction,
                userActionIntent = userActionIntent,
                userActionTriggerSrc = userActionTriggerSrc,
                userActionTriggerSrcTransform = userActionTriggerSrcTransform,
                userActionReferent = userActionReferent,
                userActionReferentTransform = userActionReferentTransform,
                userActionReferentType = userActionReferentType,
                userActionContextType = userActionContextType
            };

            LogAction(
                userAction, userActionIntent, logId, userActionTriggerSrc,
                userActionTriggerSrcTransform, DEFAULT_CONTINUOUS_ACTION_DURATION_SEC,
                userActionReferent, userActionReferentTransform, userActionReferentType,
                userActionContextType);

            //For Speech/Audio inputs
            if (userActionTriggerSrc == ActionTriggerSource.Microphone)
                audioRecordManager.RecordStart();

            return logId;
        }

        //Second (or more) time of LogContinuousAction() invocation; Using cached data.
        //If this function is not used, logs are assumed to be distinct continuous action logs, every LogContinuousAction( ) call.
        public int LogContinuousActionContinue(int existingContinuousActionLogId)
        {
            int logId = existingContinuousActionLogId;
            if (!cachedActions.ContainsKey(logId))
            {
                MsgPrintWarning($"ActionID key not present ({existingContinuousActionLogId}). Continuous Log  Data not recorded.");
                return 0;
            }

            var data = cachedActions[logId];
            LogAction(
                data.userAction, data.userActionIntent, logId, data.userActionTriggerSrc,
                data.userActionTriggerSrcTransform, DEFAULT_CONTINUOUS_ACTION_DURATION_SEC,
                data.userActionReferent, data.userActionReferentTransform, data.userActionReferentType,
                data.userActionContextType);

            return 1;
        }

        //Upon completion of Continous Action logging
        public int LogContinuousActionEnd(int existingContinuousActionLogId)
        {
            int logId = existingContinuousActionLogId;
            if (!cachedActions.ContainsKey(logId))
            {
                MsgPrintWarning($"ActionID key not present ({existingContinuousActionLogId}). Continuous Log  Data not recorded.");
                return 0;
            }

            var data = cachedActions[logId];

            //For Speech/Audio inputs
            if (data.userActionTriggerSrc == ActionTriggerSource.Microphone)
            {
                var recordedAudioFileName = audioRecordManager.RecordStop();
                data.userActionReferent = recordedAudioFileName;
            }

            LogAction(
                data.userAction, data.userActionIntent, logId, data.userActionTriggerSrc,
                data.userActionTriggerSrcTransform, DEFAULT_CONTINUOUS_ACTION_DURATION_SEC,
                data.userActionReferent, data.userActionReferentTransform, data.userActionReferentType,
                data.userActionContextType);

            //Flush cached action data
            cachedActions.Remove(logId);

            return 1;
        }
        #endregion

        #region Custom Action Logging (e.g., Audio/Microphone recording)
        //Pre-defined arguments when a developer already knows EXACTLY what to log
        public void CustomVoiceRecordLog(byte[] voiceRecordedWAVBytes, float audioDataLength) => LogAction(
                userAction: "Verbal communication",
                userActionIntent: "PostDefined",
                userActionTypeAndIdBit: (int)ActionType.Continuous,
                userActionTriggerSrc: ActionTriggerSource.Microphone,
                userActionTriggerSrcTransform: null,
                userActionDuration: audioDataLength,
                userActionReferent: voiceRecordedWAVBytes,
                userActionReferentTransform: null,
                userActionReferentType: ReferentType.Virtual,
                userActionContextType: ActionContextType.Virtual);
        #endregion


        // Generic/Base Action Logger
        public void LogAction(
            string userAction,
            string userActionIntent,
            int userActionTypeAndIdBit,
            ActionTriggerSource userActionTriggerSrc,
            Transform userActionTriggerSrcTransform,
            float userActionDuration,
            object userActionReferent,
            Transform userActionReferentTransform,
            ReferentType userActionReferentType,
            ActionContextType userActionContextType) => RecordUADData(
                action: userAction,
                actionIntent: userActionIntent,
                actionTypeAndIdBit: userActionTypeAndIdBit,
                actionTriggerSrc: userActionTriggerSrc,

                actionTransform: userActionTriggerSrcTransform,
                actionDuration: userActionDuration,

                actionReferent: userActionReferent,
                actionReferentTransform: userActionReferentTransform,
                actionReferentType: userActionReferentType,

                actionContextType: userActionContextType);

        private void RecordUADData(
            string action, string actionIntent,
            int actionTypeAndIdBit, ActionTriggerSource actionTriggerSrc, Transform actionTransform, float actionDuration,
            object actionReferent, Transform actionReferentTransform, ReferentType actionReferentType,
            ActionContextType actionContextType)
        {
            // var actionType = actionTypeAndIdBit > 0 ? ActionType.Continuous : ActionType.Discrete;
            // var logActionId = actionTypeAndIdBit;

            //5W_1H Processing
            var logStruct = new UserActionDescriptor()
            {
                ActionTypeAndIdBit = actionTypeAndIdBit,
                User = userID, //Who
                Location = (actionTransform != null) ? actionTransform.Unravel() : null, //Where
                Timestamp = curTime, //When
                Duration = actionDuration,
                UserAction = action.FirstCharacterToUpper(), //What
                UserIntent = actionIntent.FirstCharacterToUpper(), //Why
                ActionTriggerSource = actionTriggerSrc.ToString().FirstCharacterToUpper(), //How
                // ActionReferentHitPoint = actionReferentHitPoint == default ? null : actionReferentHitPoint.Unravel(), //Action target hit point

                ActionReferent = null, //path to (glb, or png)
                ActionReferentTransform = (actionReferentTransform != null) ? actionReferentTransform.Unravel() : null,
                ActionReferentType = actionReferentType.ToString(),
                ActionReferentName = "PostDefined",
                ActionContext = null, //RGBD screenshot (virtual, physical scene)
                ActionContextType = actionContextType.ToString() // Virtual, Physical (VR, AR, MR)
            };

            //Action Context Processing
            if (actionContextType == ActionContextType.Virtual) // Unity camera
            {
                if (actionTriggerSrc == ActionTriggerSource.Microphone)
                    logStruct.ActionContext = imageRecordManager.TakeVirtualCameraScreenshot();
                else
                    logStruct.ActionContext = imageRecordManager.TakeVirtualCameraScreenshot(actionReferent);
            }
            else if (actionContextType == ActionContextType.Physical) // AR Device physical camera
            {
                logStruct.ActionContext = imageRecordManager.TakePhysicalCameraScreenshot(recordDepth: true);
            }
            else //None types
                logStruct.ActionContextType = "";

            //Action Referent Processing
            if (actionTriggerSrc == ActionTriggerSource.Microphone)
            {
                if (actionReferent is byte[]) //Raw audio wav bytes
                    logStruct.ActionReferent = audioRecordManager.SaveWavBytesToPath((byte[])actionReferent);
                else if (actionReferent is string) //File path of the audio wav bytes file
                    logStruct.ActionReferent = actionReferent as string;
                else
                    MsgPrintWarning("Unknown type passed as audio input. Skipping data logging.");
            }
            else
            {
                if (actionReferentType == ReferentType.Physical) // Physical object interaction action in AR & MR
                {
                    //Take screenshot (PostDefined Data Type for AR)
                    //Already took screenshot in Action context - Reuse image.
                    if (actionContextType == ActionContextType.Physical && logStruct.ActionContext != null)
                        logStruct.ActionReferent = logStruct.ActionContext;
                    else
                        logStruct.ActionReferent = imageRecordManager.TakePhysicalCameraScreenshot(recordDepth: false);
                }
                else if (actionReferentType == ReferentType.Virtual && actionReferent != null)
                {
                    //Get gobj glb
                    var actionReferentGobj = actionReferent as GameObject;
                    //File path of the XR object glb file
                    logStruct.ActionReferent = objectManager.SaveObject(actionReferentGobj);
                    logStruct.ActionReferentName = actionReferentGobj.name;
                    logStruct.ActionReferentTransform = actionReferentGobj.transform.Unravel();
                }
                else if (actionReferentType == ReferentType.None) //None types
                {
                    logStruct.ActionReferentType = "";
                    logStruct.ActionReferentName = "";
                }
            }
            logDataList.Add(logStruct);
        }

        public void TryResumeLogSession()
        {
            if (File.Exists(dataSavePath))
                LoadXRLogFile();
        }
        public void SaveXRLogFile()
        {
            if (logDataList.Count <= 0)
            {
                MsgPrint($"Empty logs. Ignoring save attempt");
                return;
            }

            string json = JsonConvert.SerializeObject(logDataList, serializerSettings);
            File.WriteAllText(dataSavePath, json);
            MsgPrint($"Log file saved at : {dataSavePath}");
        }
        private void LoadXRLogFile()
        {
            string jsonString = File.ReadAllText(dataSavePath);
            logDataList = JsonConvert.DeserializeObject<List<UserActionDescriptor>>(jsonString, serializerSettings);
            MsgPrint($"Log file reloaded");
        }
        public List<UserActionDescriptor> LoadXRLogFile(string dataSavePath)
        {
            string jsonString = File.ReadAllText(dataSavePath);
            logDataList = JsonConvert.DeserializeObject<List<UserActionDescriptor>>(jsonString, serializerSettings);
            MsgPrint($"Log file reloaded");
            return logDataList.ToList();
        }
        public void OverrideXRLogDataList(List<UserActionDescriptor> newDataList)
        {
            logDataList = newDataList;
            MsgPrint($"Old data list overidden");
        }
    }
}