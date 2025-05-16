using UnityEngine;
using UnityEngine.InputSystem;
using Whisper.Utils;

namespace ExplainableXR.Sample
{
    namespace VR
    {
        public class ActionRecorder : MonoBehaviour
        {
            [SerializeField] private Camera userHeadCam;
            [SerializeField] private GameObject leftController;
            [SerializeField] private GameObject rightController;

            [SerializeField] private MicrophoneRecord microphoneRecord;

            private Logger EXRLogger;
            private InputAction leftControllerInputAction;
            private InputAction rightControllerInputAction;
            private ControllerTracker leftControllerTracker;
            private ControllerTracker rightControllerTracker;
            private void Start()
            {
                leftControllerInputAction = new InputAction("LeftControllerTrigger",
                InputActionType.PassThrough, "<XRController>{LeftHand}/trigger");
                rightControllerInputAction = new InputAction("RightControllerTrigger",
                InputActionType.PassThrough, "<XRController>{RightHand}/trigger");
                leftControllerInputAction.Enable();
                rightControllerInputAction.Enable();

                leftControllerTracker = new ControllerTracker(this, leftControllerInputAction,
                OnLeftTriggerPressed, OnLeftTriggerHeld, OnLeftTriggerReleased);

                rightControllerTracker = new ControllerTracker(this, rightControllerInputAction,
                OnRightTriggerPressed, OnRightTriggerHeld, OnRightTriggerReleased);

                // microphoneRecord (Default=null : requires developer's manual audio recording)
                // userID (Default=null : Automatically assigns Device-UID, but recommended to assign an ID of the user)
                EXRLogger = Logger.Initialize(this, userHeadCam, microphoneManager: microphoneRecord, userID: "User1");

                leftControllerTracker.Start();
                rightControllerTracker.Start();
            }

            #region Left Controller Interaction
            int leftControllerActionID;
            private int OnLeftTriggerPressed(ReferentMetadata referentMetadata)
            {
                Debug.Log("Left pressed");
                var colliders = Physics.OverlapSphere(leftController.transform.position, 0.01f, LayerMask.GetMask("Target"));
                if (colliders.Length > 0)
                {
                    var hitGobj = colliders[0].gameObject;
                    hitGobj.transform.SetPositionAndRotation(leftController.transform.position, leftController.transform.rotation);
                    referentMetadata.Gobj = hitGobj;
                    referentMetadata.interactionType = InteractionType.Direct;

                    leftControllerActionID = EXRLogger.LogContinuousActionBegin(
                        userAction: "Directly Grab",
                        userActionIntent: "Directly grabbing an object",
                        userActionTriggerSrc: ActionTriggerSource.XRController_L,
                        userActionTriggerSrcTransform: leftController.transform,
                        userActionReferent: hitGobj,
                        userActionReferentTransform: hitGobj.transform,
                        userActionReferentType: ReferentType.Virtual,
                        userActionContextType: ActionContextType.Virtual);
                    return 1;
                }
                else if (Physics.Raycast(leftController.transform.position, leftController.transform.forward, out var hitInfo, 10f, LayerMask.GetMask("Target")))
                {
                    var hitGobj = hitInfo.collider.gameObject;
                    hitGobj.transform.SetPositionAndRotation(leftController.transform.position, leftController.transform.rotation);
                    referentMetadata.Gobj = hitGobj;
                    referentMetadata.interactionType = InteractionType.Indirect;

                    leftControllerActionID = EXRLogger.LogContinuousActionBegin(
                        userAction: "Grab Distant",
                        userActionIntent: "Grabbing a distant object with ray selector",
                        userActionTriggerSrc: ActionTriggerSource.XRController_L,
                        userActionTriggerSrcTransform: leftController.transform,
                        userActionReferent: hitGobj,
                        userActionReferentTransform: hitGobj.transform,
                        userActionReferentType: ReferentType.Virtual,
                        userActionContextType: ActionContextType.Virtual);
                    return 2;
                }

                return 0;
            }
            private int OnLeftTriggerHeld((ReferentMetadata referentMetadata, bool record) args)
            {
                if (args.record)
                {
                    EXRLogger.LogContinuousActionContinue(leftControllerActionID);
                }
                if (args.referentMetadata.Gobj != null)
                {
                    args.referentMetadata.Gobj.transform.SetPositionAndRotation(
                        leftController.transform.position, leftController.transform.rotation);
                    return 1;
                }
                return 0;
            }
            private int OnLeftTriggerReleased(ReferentMetadata referentMetadata)
            {
                EXRLogger.LogContinuousActionEnd(leftControllerActionID);

                if (referentMetadata.Gobj != null)
                {
                    referentMetadata.Gobj.transform.SetPositionAndRotation(leftController.transform.position, leftController.transform.rotation);
                    referentMetadata.Gobj = null;
                    referentMetadata.interactionType = InteractionType.None;
                    return 1;
                }

                return 0;
            }
            #endregion

            #region Right Controller Interaction
            int rightControllerActionID;
            private int OnRightTriggerPressed(ReferentMetadata referentMetadata)
            {
                Debug.Log("Right pressed");
                var colliders = Physics.OverlapSphere(rightController.transform.position, 0.01f, LayerMask.GetMask("Target"));
                if (colliders.Length > 0)
                {
                    var hitGobj = colliders[0].gameObject;
                    hitGobj.transform.SetPositionAndRotation(rightController.transform.position, rightController.transform.rotation);
                    referentMetadata.Gobj = hitGobj;
                    referentMetadata.interactionType = InteractionType.Direct;

                    rightControllerActionID = EXRLogger.LogContinuousActionBegin(
                        userAction: "Directly Grab",
                        userActionIntent: "Directly grabbing an object",
                        userActionTriggerSrc: ActionTriggerSource.XRController_R,
                        userActionTriggerSrcTransform: rightController.transform,
                        userActionReferent: hitGobj,
                        userActionReferentTransform: hitGobj.transform,
                        userActionReferentType: ReferentType.Virtual,
                        userActionContextType: ActionContextType.Virtual);
                    return 1;
                }
                else if (Physics.Raycast(rightController.transform.position, rightController.transform.forward, out var hitInfo, 10f, LayerMask.GetMask("Target")))
                {
                    var hitGobj = hitInfo.collider.gameObject;
                    hitGobj.transform.SetPositionAndRotation(rightController.transform.position, rightController.transform.rotation);
                    referentMetadata.Gobj = hitGobj;
                    referentMetadata.interactionType = InteractionType.Indirect;

                    rightControllerActionID = EXRLogger.LogContinuousActionBegin(
                        userAction: "Grab Distant",
                        userActionIntent: "Grabbing a distant object with ray selector",
                        userActionTriggerSrc: ActionTriggerSource.XRController_R,
                        userActionTriggerSrcTransform: rightController.transform,
                        userActionReferent: hitGobj,
                        userActionReferentTransform: hitGobj.transform,
                        userActionReferentType: ReferentType.Virtual,
                        userActionContextType: ActionContextType.Virtual);
                    return 2;
                }

                return 0;
            }
            private int OnRightTriggerHeld((ReferentMetadata referentMetadata, bool record) args)
            {
                if (args.record)
                {
                    EXRLogger.LogContinuousActionContinue(rightControllerActionID);
                }
                if (args.referentMetadata.Gobj != null)
                {
                    args.referentMetadata.Gobj.transform.SetPositionAndRotation(
                        rightController.transform.position, rightController.transform.rotation);
                    return 1;
                }
                return 0;
            }
            private int OnRightTriggerReleased(ReferentMetadata referentMetadata)
            {
                EXRLogger.LogContinuousActionEnd(rightControllerActionID);

                if (referentMetadata.Gobj != null)
                {
                    referentMetadata.Gobj.transform.SetPositionAndRotation(rightController.transform.position, rightController.transform.rotation);
                    referentMetadata.Gobj = null;
                    referentMetadata.interactionType = InteractionType.None;
                    return 1;
                }

                return 0;
            }
            #endregion

            private void OnDisable()
            {
                leftControllerTracker.Stop();
                rightControllerTracker.Stop();
                leftControllerInputAction.Disable();
                rightControllerInputAction.Disable();
            }

#if UNITY_EDITOR
            private void OnDestroy()
            {
                EXRLogger?.SaveXRLogFile();
            }
#else
            private void OnApplicationPause(bool pauseStatus)
            {
                if (pauseStatus)
                {
                    EXRLogger?.SaveXRLogFile();
                }
                else
                    EXRLogger.TryResumeLogSession();
            }
#endif
        }
    }
}