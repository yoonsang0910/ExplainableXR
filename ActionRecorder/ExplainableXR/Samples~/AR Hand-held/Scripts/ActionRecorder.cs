using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using System;
using UnityEngine.XR.ARFoundation;
using System.Linq;

namespace ExplainableXR.Sample
{
    namespace AR
    {
        public class ActionRecorder : MonoBehaviour
        {
            [SerializeField] private Camera userCenterCam;
            [SerializeField] private ARCameraManager arCameraManager;
            [SerializeField] private AROcclusionManager occlusionManager;
            [SerializeField] private ARRaycastManager raycastManager;

            [SerializeField] private GameObject arMarkerPrefab;
            [SerializeField] private GameObject unityMarkerPrefab;
            [SerializeField] private GameObject arMemoPrefab;

            private Logger EXRLogger;
            private TransformTracker transformTracker;
            private void Start()
            {
                transformTracker = new TransformTracker(this, userCenterCam.transform,
                OnARDeviceMovementBegin, OnARDeviceMovementContinue, OnARDeviceMovementEnd,
                actionInvokeInterval: 1.0f,
                posSensitivity: 0.20f, rotSensitivity: 25);

                // microphoneRecord (Default=null : developer can optionally add audio recording - Refer to VR Sample's Custom Voice Recorder scene)
                // userID (Default=null : Automatically assigns Device-UID, but recommended to assign an ID of the user)
                EXRLogger = Logger.Initialize(this, userCenterCam, arCameraManager, occlusionManager,
                microphoneManager: null, userID: null);

                transformTracker.Start();
            }

            #region AR Device Transform Tracking
            public int OnARDeviceMovementBegin() => EXRLogger.LogContinuousActionBegin(
                userAction: "Navigate",
                userActionIntent: "User walking around in physical space",
                userActionTriggerSrc: ActionTriggerSource.ARHandHeld,
                userActionTriggerSrcTransform: userCenterCam.transform,
                userActionReferent: null,
                userActionReferentTransform: null,
                userActionReferentType: ReferentType.None,
                userActionContextType: ActionContextType.Physical);
            public int OnARDeviceMovementContinue(int actionId) => EXRLogger.LogContinuousActionContinue(actionId);
            public int OnARDeviceMovementEnd(int actionId) => EXRLogger.LogContinuousActionEnd(actionId);
            #endregion

            #region AR Device User Touch Inputs
            public void OnPhysicalSurfaceARRayHitButtonPress()
            {
                var transform = userCenterCam.transform;
                var origin = transform.position;
                var dir = transform.forward;
                var ray = new Ray(origin, dir);
                var rayHitResList = new List<ARRaycastHit>();
                if (raycastManager.Raycast(ray, rayHitResList, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinBounds))
                {
                    var rayHitRes = rayHitResList[0];
                    var hitGobj = Instantiate(arMarkerPrefab, rayHitRes.pose.position, Quaternion.identity);
                    EXRLogger.LogDiscreteAction(
                        userAction: "Place Marker",
                        userActionIntent: "Marking walkable paths in user's physical environment",
                        userActionTriggerSrc: ActionTriggerSource.XRHMD,
                        userActionTriggerSrcTransform: userCenterCam.transform,
                        userActionReferent: hitGobj,
                        userActionReferentTransform: hitGobj.transform,
                        userActionReferentType: ReferentType.Physical,
                        userActionContextType: ActionContextType.Physical);
                }
            }
            public void OnVirtualObjectUnityRayHitButtonPress()
            {
                var transform = userCenterCam.transform;
                var origin = transform.position;
                var dir = transform.forward;
                var ray = new Ray(origin, dir);
                var rayHitResArr = Physics.RaycastAll(ray, 50f, LayerMask.GetMask("AR Ray Object"));
                if (rayHitResArr.Length > 0)
                {
                    var hitGobj = rayHitResArr[0].collider.gameObject;
                    var tmpHitIndicatorGobj = Instantiate(unityMarkerPrefab);
                    tmpHitIndicatorGobj.transform.position = rayHitResArr[0].point;
                    Destroy(tmpHitIndicatorGobj, 1.5f);

                    EXRLogger.LogDiscreteAction(
                        userAction: "Inspection Check",
                        userActionIntent: "Placing an additional indicator on an existing virtual marker",
                        userActionTriggerSrc: ActionTriggerSource.XRHMD,
                        userActionTriggerSrcTransform: userCenterCam.transform,
                        userActionReferent: hitGobj,
                        userActionReferentTransform: hitGobj.transform,
                        userActionReferentType: ReferentType.Virtual,
                        userActionContextType: ActionContextType.Physical);
                }

            }
            public void OnFloatingARMemoGenerateButtonPress()
            {
                var memoGobj = Instantiate(arMemoPrefab);
                memoGobj.transform.position =
                userCenterCam.transform.position +
                userCenterCam.transform.forward * 0.5f; // (50cm forward from current cam pose)
                memoGobj.transform.rotation = userCenterCam.transform.rotation;
                memoGobj.AddComponent<ARAnchor>();
                EXRLogger.LogDiscreteAction(
                    userAction: "Generate Memo",
                    userActionIntent: "Situating floating virtual marker at AR device's location",
                    userActionTriggerSrc: ActionTriggerSource.ARHandHeld,
                    userActionTriggerSrcTransform: userCenterCam.transform,
                    userActionReferent: memoGobj,
                    userActionReferentTransform: memoGobj.transform,
                    userActionReferentType: ReferentType.Virtual,
                    userActionContextType: ActionContextType.Physical);
            }
            #endregion

            private void OnDisable()
            {
                transformTracker.Stop();
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
