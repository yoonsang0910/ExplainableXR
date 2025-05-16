using System.Collections;
using UnityEngine;

namespace ExplainableXR.Sample
{
    public abstract class Tracker
    {
        private MonoBehaviour monobehavior = null;
        private float eventLoopInvokeInterval = 1.0f; //in Secs
        private Coroutine eventLoopInvoker = null;

        protected Tracker(MonoBehaviour monobehavior, float eventLoopInvokeInterval)
        {
            this.monobehavior = monobehavior;
            this.eventLoopInvokeInterval = eventLoopInvokeInterval;
        }
        ~Tracker()
        {
            Stop();
        }

        public void Start()
        {
            if (eventLoopInvoker == null)
                eventLoopInvoker = monobehavior.StartCoroutine(RunEventLoop());
        }

        public void Stop()
        {
            if (eventLoopInvoker != null)
            {
                monobehavior.StopCoroutine(eventLoopInvoker);
                eventLoopInvoker = null;
            }
        }

        private IEnumerator RunEventLoop()
        {
            var waitIntervalCondition = new WaitForSecondsRealtime(eventLoopInvokeInterval);
            while (true)
            {
                yield return waitIntervalCondition;
                InvokeTrackEvent();
            }
        }
        protected abstract void InvokeTrackEvent();
    }
}