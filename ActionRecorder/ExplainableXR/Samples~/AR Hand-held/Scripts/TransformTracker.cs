using System;
using ExplainableXR;
using Unity.VisualScripting;
using UnityEngine;

namespace ExplainableXR.Sample
{
    public class TransformTracker : Tracker
    {
        private Transform targetGobjTransform;
        private Func<int> continuousActionBegin;
        private Func<int, int> continuousActionContinue;
        private Func<int, int> continuousActionEnd;
        private float posSensitivity;
        private float rotSensitivity;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private bool isMoving = false;
        private int actionId;

        public TransformTracker(
            MonoBehaviour monoBehaviour,
            Transform targetGobjTransform, // Invoke condition
            Func<int> continuousActionBegin, // Invoke target func1.
            Func<int, int> continuousActionContinue, // Invoke target func2.
            Func<int, int> continuousActionEnd, // Invoke target func3.
            float actionInvokeInterval = 0.5f, //in Secs
            float posSensitivity = 0.075f, // in Meters (Unity) : 7.5cm
            float rotSensitivity = 15f) // in Euler angle
            : base(monoBehaviour, actionInvokeInterval)
        {
            this.targetGobjTransform = targetGobjTransform;
            this.continuousActionBegin = continuousActionBegin;
            this.continuousActionContinue = continuousActionContinue;
            this.continuousActionEnd = continuousActionEnd;

            this.posSensitivity = posSensitivity;
            this.rotSensitivity = rotSensitivity;
            lastPosition = targetGobjTransform.position;
            lastRotation = targetGobjTransform.rotation;
        }

        protected override void InvokeTrackEvent()
        {
            if (HasTransformChanged()) //Invoke condition
            {
                if (!isMoving)
                {
                    actionId = continuousActionBegin.Invoke();
                    isMoving = true;
                }
                else
                    continuousActionContinue.Invoke(actionId);
            }
            else if (isMoving)
            {
                continuousActionEnd.Invoke(actionId);
                isMoving = false;
            }
        }

        private bool HasTransformChanged()
        {
            float dist = Vector3.Distance(targetGobjTransform.position, lastPosition);
            float ang = Quaternion.Angle(targetGobjTransform.rotation, lastRotation);
            if (dist > posSensitivity || ang > rotSensitivity)
            {
                lastPosition = targetGobjTransform.position;
                lastRotation = targetGobjTransform.rotation;
                return true;
            }
            return false;
        }
    }
}