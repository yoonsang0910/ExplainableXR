//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ExplainableXR
{
    using UnityEngine;
    using Unity.XR.CoreUtils;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.XR;


    public class TemplateBasedLogging : MonoBehaviour
    {
        [SerializeField] private Camera userCenterEyeGobj;
        private Logger EXRLogger;

        private UnityEngine.InputSystem.InputAction m_Keyboard_Pressenter_Enter;

        private UnityEngine.InputSystem.InputAction m_Keyboard_Pressesc_Escape;

        private UnityEngine.InputSystem.InputAction m_Keyboard_Pressspace_Space;

        private void Awake()
        {
            m_Keyboard_Pressenter_Enter = new InputAction("Keyboard_Pressenter_Enter", InputActionType.Button, "<Keyboard>/enter");
            m_Keyboard_Pressesc_Escape = new InputAction("Keyboard_Pressesc_Escape", InputActionType.Button, "<Keyboard>/escape");
            m_Keyboard_Pressspace_Space = new InputAction("Keyboard_Pressspace_Space", InputActionType.Button, "<Keyboard>/space");

            EXRLogger = Logger.Initialize(this, userCenterEyeGobj, userID: "User1");
        }

        private bool OnPressenter_Startrecording_Logging_Condition(InputAction.CallbackContext context)
        {
            object value = context.ReadValueAsObject();
            string valueType = Utility.TypeToStringName(value.GetType());
            Debug.Log($"Action : {context.action.name}, Value : {value}, Typeof({valueType})");

            // Insert logging condition logic here...
            if (true)
            {
                return true;
            }
            return false;
        }

        private bool OnPressesc_Endrecording_Logging_Condition(InputAction.CallbackContext context)
        {
            object value = context.ReadValueAsObject();
            string valueType = Utility.TypeToStringName(value.GetType());
            Debug.Log($"Action : {context.action.name}, Value : {value}, Typeof({valueType})");

            // Insert logging condition logic here...
            if (true)
            {
                return true;
            }
            return false;
        }

        private bool OnPressspace_Testrecorddiscrete_Logging_Condition(InputAction.CallbackContext context)
        {
            object value = context.ReadValueAsObject();
            string valueType = Utility.TypeToStringName(value.GetType());
            Debug.Log($"Action : {context.action.name}, Value : {value}, Typeof({valueType})");

            // Insert logging condition logic here...
            if (true)
            {
                return true;
            }
            return false;
        }

        private int actionID;
        private void LogXRUserData(InputAction.CallbackContext context, string userAction, string userActionIntent)
        {
            // Insert log data logic here...
            switch (userAction)
            {
                case "Press Enter":
                    actionID = EXRLogger.LogContinuousActionBegin(userAction, userActionIntent,
                    ActionTriggerSource.XRHMD, null, null, null,
                    ReferentType.None, ActionContextType.Virtual);
                    break;
                case "Press ESC":
                    EXRLogger.LogContinuousActionEnd(actionID);
                    break;
                case "Press Space":
                    EXRLogger.LogDiscreteAction(userAction, userActionIntent,
                    ActionTriggerSource.XRHMD, null, null, null,
                    ReferentType.None, ActionContextType.Virtual);
                    break;
            }

            Debug.Log($"[Logging XR User Data] User Action : {userAction}, Intent : {userActionIntent})");
        }

        private void OnPressenter_Startrecording(InputAction.CallbackContext context)
        {
            if (OnPressenter_Startrecording_Logging_Condition(context))
            {
                LogXRUserData(context, "Press Enter", "Start Recording");
            }
        }

        private void OnPressesc_Endrecording(InputAction.CallbackContext context)
        {
            if (OnPressesc_Endrecording_Logging_Condition(context))
            {
                LogXRUserData(context, "Press ESC", "End Recording");
            }
        }

        private void OnPressspace_Testrecorddiscrete(InputAction.CallbackContext context)
        {
            if (OnPressspace_Testrecorddiscrete_Logging_Condition(context))
            {
                LogXRUserData(context, "Press Space", "Test Record Discrete");
            }
        }

        private void OnEnable()
        {
            m_Keyboard_Pressenter_Enter.Enable();
            m_Keyboard_Pressenter_Enter.performed += OnPressenter_Startrecording;
            m_Keyboard_Pressesc_Escape.Enable();
            m_Keyboard_Pressesc_Escape.performed += OnPressesc_Endrecording;
            m_Keyboard_Pressspace_Space.Enable();
            m_Keyboard_Pressspace_Space.performed += OnPressspace_Testrecorddiscrete;
        }

        private void OnDisable()
        {
            m_Keyboard_Pressenter_Enter.Disable();
            m_Keyboard_Pressenter_Enter.performed -= OnPressenter_Startrecording;
            m_Keyboard_Pressesc_Escape.Disable();
            m_Keyboard_Pressesc_Escape.performed -= OnPressesc_Endrecording;
            m_Keyboard_Pressspace_Space.Disable();
            m_Keyboard_Pressspace_Space.performed -= OnPressspace_Testrecorddiscrete;
        }

        private void Start()
        {
            // Start logic here
        }

        private void Update()
        {
            // Update logic here
        }
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
