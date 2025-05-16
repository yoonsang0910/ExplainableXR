using UnityEngine;
using UnityEngine.InputSystem;
using Whisper.Utils;

namespace ExplainableXR.Sample
{
    namespace Basics
    {
        public class ActionRecorder : MonoBehaviour
        {
            [SerializeField] private Camera userCenterEyeGobj;
            [SerializeField] private MicrophoneRecord microphoneRecord;

            private Logger EXRLogger;
            private int voiceRecordingActionId;
            private void Start()
            {
                // microphoneRecord (Default=null : if null, developer needs to manually audio record)
                // userID (Default=null : Automatically assigns Device-UID, but recommended to assign an ID of the user)
                EXRLogger = Logger.Initialize(this, userCenterEyeGobj, microphoneManager: microphoneRecord, userID: "User1");
            }
            private void Update()
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame ||
                Keyboard.current.numpad1Key.wasPressedThisFrame) // Upon Num1 key press
                {
                    if (Physics.Raycast(
                        userCenterEyeGobj.transform.position,
                        userCenterEyeGobj.transform.forward,
                        out var hitInfo, 100f, LayerMask.GetMask("Red Layer")))
                    {
                        EXRLogger.LogDiscreteAction(
                            userAction: "PressDigitKey",
                            userActionIntent: "Test 'logging functionality 1' of a data logger",
                            userActionTriggerSrc: ActionTriggerSource.XRHMD,
                            userActionTriggerSrcTransform: userCenterEyeGobj.transform,
                            userActionReferent: hitInfo.collider.gameObject,
                            userActionReferentTransform: hitInfo.transform,
                            userActionReferentType: ReferentType.Virtual,
                            userActionContextType: ActionContextType.Virtual);
                    }
                }
                else if (Keyboard.current.digit2Key.wasPressedThisFrame ||
                Keyboard.current.numpad2Key.wasPressedThisFrame) // Upon Num2 key press
                {
                    if (Physics.Raycast(
                        userCenterEyeGobj.transform.position,
                        userCenterEyeGobj.transform.forward,
                        out var hitInfo, 100f, LayerMask.GetMask("Green Layer")))
                    {
                        EXRLogger.LogDiscreteAction(
                            userAction: "PressDigitKey",
                            userActionIntent: "Test 'logging functionality 2' of a data logger",
                            userActionTriggerSrc: ActionTriggerSource.XRHMD,
                            userActionTriggerSrcTransform: userCenterEyeGobj.transform,
                            userActionReferent: hitInfo.collider.gameObject,
                            userActionReferentTransform: hitInfo.transform,
                            userActionReferentType: ReferentType.Virtual,
                            userActionContextType: ActionContextType.Virtual);
                    }
                }
                else if (Keyboard.current.enterKey.wasPressedThisFrame) // Upon Enter key press
                {
                    voiceRecordingActionId =
                    EXRLogger.LogContinuousActionBegin( //Voice recording start
                        userAction: "Voice",
                        userActionIntent: "PostDefined",
                        userActionTriggerSrc: ActionTriggerSource.Microphone,
                        userActionTriggerSrcTransform: userCenterEyeGobj.transform,
                        userActionReferent: null,
                        userActionReferentTransform: null,
                        userActionReferentType: ReferentType.Virtual,
                        userActionContextType: ActionContextType.Virtual);
                }
                else if (Keyboard.current.escapeKey.wasPressedThisFrame) // Upon ESC key press
                {
                    EXRLogger.LogContinuousActionEnd(voiceRecordingActionId); //Voice recording stop
                }
            }
            // <<DO NOT FORGET TO ADD THIS PART!>> if not, the data will not be saved.
#if UNITY_EDITOR
            private void OnDestroy() => EXRLogger?.SaveXRLogFile();
#else
            private void OnApplicationPause(bool pauseStatus)
            {
                if (pauseStatus)
                    EXRLogger?.SaveXRLogFile();
                else
                    EXRLogger.TryResumeLogSession();
            }
#endif
        }
    }
}