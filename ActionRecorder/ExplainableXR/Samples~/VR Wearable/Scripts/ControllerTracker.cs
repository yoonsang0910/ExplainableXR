using System;
using System.Collections;
using ExplainableXR.Sample.VR;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ExplainableXR.Sample
{
    public enum InteractionType
    {
        None,
        Direct,
        Indirect
    }
    public class ReferentMetadata
    {
        public GameObject Gobj;
        public InteractionType interactionType;
    }

    public class ControllerTracker : Tracker
    {
        private InputAction inputAction;
        private Func<ReferentMetadata, int> controllerPressed;
        private Func<(ReferentMetadata, bool), int> controllerPressHeld;
        private Func<ReferentMetadata, int> controllerReleased;
        private float pressThreshold;
        private float holdInterval;
        private bool isPressed;
        private float lastHeldTime;
        private ReferentMetadata referentMetadata = new();

        public ControllerTracker(
            MonoBehaviour monoBehaviour,
            InputAction inputAction, // Invoke condition
            Func<ReferentMetadata, int> controllerPressed, // Invoke target func1.
            Func<(ReferentMetadata, bool), int> controllerPressHeld, // Invoke target func2.
            Func<ReferentMetadata, int> controllerReleased, // Invoke target func3.
            float actionInvokeInterval = 0.05f,//in Secs (20FPS)
            float pressThreshold = 0.25f,
            float holdInterval = 1.0f) //in Secs
            : base(monoBehaviour, actionInvokeInterval)
        {
            this.inputAction = inputAction;
            this.controllerPressed = controllerPressed;
            this.controllerPressHeld = controllerPressHeld;
            this.controllerReleased = controllerReleased;

            this.pressThreshold = pressThreshold;
            this.holdInterval = holdInterval;
            isPressed = false;
            lastHeldTime = 0f;
        }

        protected override void InvokeTrackEvent()
        {
            var triggerValue = inputAction.ReadValue<float>();

            if (!isPressed && triggerValue > pressThreshold) // Trigger pressed
            {
                isPressed = true;
                lastHeldTime = Time.time;
                controllerPressed.Invoke(referentMetadata);
            }
            if (isPressed)
            {
                //Record every "holdInterval" secs (otherwise, it's invoked too often..)
                if (Time.time - lastHeldTime >= holdInterval)
                {
                    lastHeldTime = Time.time;
                    controllerPressHeld.Invoke((referentMetadata, true));
                }
                else //Don't really record but update the position of the referent in the scene
                    controllerPressHeld.Invoke((referentMetadata, false));

                if (triggerValue <= pressThreshold) // Trigger released
                {
                    isPressed = false;
                    controllerReleased.Invoke(referentMetadata);
                }
            }
        }
    }
}