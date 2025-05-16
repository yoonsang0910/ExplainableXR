using UnityEngine;
using UnityEngine.InputSystem;
using Whisper.Utils;

namespace ExplainableXR.Sample
{
    namespace Basics
    {
        public class CustomVoiceRecording : MonoBehaviour
        {
            [SerializeField] private Camera userCenterEyeGobj;
            [SerializeField] private MicrophoneRecord microphoneRecord;
            [SerializeField] private CustomVoiceRecorder customVoiceRecorder;

            private Logger EXRLogger;
            private void Start()
            {
                // Unassigned userID in this example (falls back to unique device ID, but not recommended)
                // Here, note that we manually initialize (overriding) the Microphone Manager, the 'microphoneManager' arg, may be null
                EXRLogger = Logger.Initialize(this, userCenterEyeGobj, microphoneManager: null); 
                customVoiceRecorder.OnVoiceQueryEnd += OnVoiceRecordComplete;
            }
            private void Update()
            {
                if (Keyboard.current.enterKey.wasPressedThisFrame) // Upon Enter key press
                {
                    customVoiceRecorder.RecordStart(); //Voice recording start
                }
                else if (Keyboard.current.escapeKey.wasPressedThisFrame) // Upon ESC key press
                {
                    customVoiceRecorder.RecordStop(); //Voice recording stop (Upon stop, echoes back the recording.)
                }
            }
            public void OnVoiceRecordComplete(byte[] voiceWavBytes, float recordedAudioLength)
            {
                print("This function is invoked upon completion of voice recording");

                //Record voice input with direct file.
                //(the voice is transcribed at the Action Processor stage, for optimal performance)
                EXRLogger.LogAction(
                    userAction: "Verbal communication",
                    userActionIntent: "PostDefined",
                    userActionTypeAndIdBit: (int)ActionType.Continuous,
                    userActionTriggerSrc: ActionTriggerSource.Microphone,
                    userActionTriggerSrcTransform: null,
                    userActionDuration: recordedAudioLength,
                    userActionReferent: voiceWavBytes,
                    userActionReferentTransform: null,
                    userActionReferentType: ReferentType.Virtual,
                    userActionContextType: ActionContextType.Virtual);
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