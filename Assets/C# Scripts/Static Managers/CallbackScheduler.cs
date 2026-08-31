using Fire_Pixel.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;


namespace Fire_Pixel.Utility
{
    /// <summary>
    /// Uitlity class to have an optimized easy access to varying callbacks by using an Action based callback system
    /// Handles callbacks and batch them for every script by an event based register system
    /// </summary>
    public static class CallbackScheduler
    {
        private static event Action Update;
        private static event Action LateUpdate;
        private static event Action FixedUpdate;

        private static event Action LateDestroy;
        private static event Action ApplicationQuit;
        private static event Action LateApplicationQuit;

        private static readonly List<DelayedCallback> delayedCallbacks = new List<DelayedCallback>();
        private static readonly List<InvokeCallbackReference> callbackReferences = new List<InvokeCallbackReference>();

        private static bool quitting;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Reset();

            CallbackRunnerInstance gameManager = new GameObject(">>CalbackScheduler<<").AddComponent<CallbackRunnerInstance>();
            gameManager.Init();

            GameObject.DontDestroyOnLoad(gameManager.gameObject);
        }
        public static void Reset()
        {
            Update = null;
            LateUpdate = null;
            FixedUpdate = null;

            LateDestroy = null;
            ApplicationQuit = null;
            LateApplicationQuit = null;

            delayedCallbacks?.Clear();
            callbackReferences?.Clear();
            quitting = false;
        }


        #region Register/UnRegister/Manage Callbacks

        /// <summary>
        /// Register a method to call every frame like Update()
        /// </summary>
        public static void RegisterCallback(CallbackType type, Action action)
        {
            switch (type)
            {
                case CallbackType.Update:
                    Update += action;
                    return;

                case CallbackType.LateUpdate:
                    LateUpdate += action;
                    return;

                case CallbackType.FixedUpdate:
                    FixedUpdate += action;
                    return;

                case CallbackType.Destroy:
                    LateDestroy += action;
                    return;

                case CallbackType.LateDestroy:
                    LateDestroy += action;
                    return;

                case CallbackType.ApplicationQuit:
                    ApplicationQuit += action;
                    return;

                case CallbackType.LateApplicationQuit:
                    LateApplicationQuit += action;
                    return;

                default:
                    DebugLogger.LogError($"CallbackType {type} doesnt exist. Register failed.");
                    return;
            }
        }

        /// <summary>
        /// Unregister a registered method for callback "<paramref name="type"/>"
        /// </summary>
        public static void UnRegisterCallback(CallbackType type, Action action)
        {
            switch (type)
            {
                case CallbackType.Update:
                    Update -= action;
                    return;

                case CallbackType.LateUpdate:
                    LateUpdate -= action;
                    return;

                case CallbackType.FixedUpdate:
                    FixedUpdate -= action;
                    return;

                case CallbackType.Destroy:
                    LateDestroy -= action;
                    return;

                case CallbackType.LateDestroy:
                    LateDestroy -= action;
                    return;

                case CallbackType.ApplicationQuit:
                    ApplicationQuit -= action;
                    return;

                case CallbackType.LateApplicationQuit:
                    LateApplicationQuit -= action;
                    return;

                default:
                    DebugLogger.LogError($"CallbackType {type} doesnt exist. Unregister failed.");
                    return;
            }
        }

        /// <summary>
        /// Register or Unregister a method for callback "<paramref name="type"/>" based on bool <paramref name="doRegister"/>
        /// </summary>
        public static void ManageCallback(CallbackType type, Action action, bool doRegister)
        {
            if (doRegister)
            {
                RegisterCallback(type, action);
            }
            else
            {
                UnRegisterCallback(type, action);
            }
        }

        #endregion


        #region Delayed Callback System

        public static InvokeCallbackReference InvokeDelayed(float delay, Action callback, int groupId = 0)
        {
            delayedCallbacks.Add(new DelayedCallback(callback, Time.time + delay, groupId));

            InvokeCallbackReference callbackRef = new InvokeCallbackReference(delayedCallbacks.Count - 1);
            callbackReferences.Add(callbackRef);

            return callbackRef;
        }
        /// <summary>
        /// Stops a previously scheduled Invoke Callback by ref and clears its reference.
        /// </summary>
        public static void CancelInvoke(ref InvokeCallbackReference callbackRef)
        {
            if (callbackRef == null) return;

            RemoveDelayedCallback(callbackRef.Id);

            // Destroy callback reference
            callbackRef = null;
        }
        /// <summary>
        /// Cancel all invokes with the same group id, useful to cancel all callbacks of a script for example when it gets destroyed without having to save every callback reference
        /// </summary>
        public static void CancelAllInvokesInGroup(int groupId)
        {
            for (int i = delayedCallbacks.Count - 1; i >= 0; i--)
            {
                if (delayedCallbacks[i].GroupId == groupId)
                {
                    RemoveDelayedCallback(i);
                }
            }
        }

        /// <summary>
        /// Remove delayed callback and its reference by id
        /// </summary>
        private static void RemoveDelayedCallback(int toRemoveId)
        {
            // If the callback to remove is not the last one, update the last callback in list id to match new position after SwapBack
            if (toRemoveId != delayedCallbacks.Count - 1)
            {
                // Update the reference of the moved callback
                callbackReferences[^1].SetId(toRemoveId);
            }
            // Remove the callback and its reference
            callbackReferences.RemoveAtSwapBack(toRemoveId);
            delayedCallbacks.RemoveAtSwapBack(toRemoveId);
        }

        #endregion


        /// <summary>
        /// Callback runner instance to invoke the registered callbacks.
        /// </summary>
        private class CallbackRunnerInstance : MonoBehaviour
        {
            public void Init()
            {
                StartCoroutine(UpdateLoop());
            }
            private IEnumerator UpdateLoop()
            {
                float fixedAccumulator = 0f;
                float fixedDelta = Time.fixedDeltaTime;

                while (true)
                {
                    // Update
                    Update?.Invoke();

                    // Invoke delayed callbacks
                    float time = Time.time;
                    for (int i = delayedCallbacks.Count - 1; i >= 0; i--)
                    {
                        if (time >= delayedCallbacks[i].InvokeGlobalTime)
                        {
                            Action callback = delayedCallbacks[i].Callback;
                            callback?.Invoke();
                            RemoveDelayedCallback(i);
                        }
                    }

                    // FixedUpdate
                    fixedAccumulator += Time.deltaTime;
                    while (fixedAccumulator >= fixedDelta)
                    {
                        FixedUpdate?.Invoke();
                        fixedAccumulator -= fixedDelta;
                    }

                    // LateUpdate
                    LateUpdate?.Invoke();

                    // Also LateUpdate, but clears all subscribers
                    LateDestroy?.Invoke();
                    LateDestroy = null;

                    if (quitting)
                    {
                        LateApplicationQuit?.Invoke();
                        LateApplicationQuit = null;
                        StopAllCoroutines();
                        yield break;
                    }

                    yield return null;
                }
            }

            private void OnApplicationQuit()
            {
                ApplicationQuit?.Invoke();
                quitting = true;
            }
            private void OnDestroy()
            {
                Update = null;
                LateUpdate = null;
                FixedUpdate = null;

                LateDestroy = null;
                LateApplicationQuit = null;
            }
        }
    }

    public static class CallbackSchedulerExtensionMethods
    {
        /// <summary>
        /// Invoke function <paramref name="f"/> after <paramref name="delay"/> seconds. Schedules a coroutine on the target <see cref="MonoBehaviour"/>
        /// </summary>
        /// <returns>The scheduled coroutine ref</returns>
        public static InvokeCallbackReference Invoke(this MonoBehaviour mb, float delay, Action f)
        {
            return CallbackScheduler.InvokeDelayed(delay, f, mb.GetInstanceID());
        }
        /// <summary>
        /// Stops a previously scheduled Invoke Callback on target (<see cref="MonoBehaviour"/>) and clears its reference.
        /// Must be called on the same owner (<see cref="MonoBehaviour"/>) that started the coroutine.
        /// </summary>
        public static void CancelInvoke(this MonoBehaviour _, InvokeCallbackReference callbackRef)
        {
            if (callbackRef == null) return;

            CallbackScheduler.CancelInvoke(ref callbackRef);
        }
        /// <summary>
        /// Stops a previously scheduled Invoke Callback on <see cref="CallbackScheduler"/> and clears its reference.
        /// </summary>
        public static void CancelAllInvokes(this MonoBehaviour mb)
        {
            CallbackScheduler.CancelAllInvokesInGroup(mb.GetInstanceID());
        }
    }

    [System.Serializable]
    public struct DelayedCallback
    {
        public Action Callback;
        public float InvokeGlobalTime;
        public int GroupId;

        public DelayedCallback(Action callback, float invokeGlobalTime, int groupId)
        {
            Callback = callback;
            InvokeGlobalTime = invokeGlobalTime;
            GroupId = groupId;
        }
    }
    [System.Serializable]
    public class InvokeCallbackReference
    {
        public int Id { get; private set; }
        public void SetId(int id) => Id = id;

        public InvokeCallbackReference(int id)
        {
            Id = id;
        }
    }

    public enum CallbackType : byte
    {
        Update,
        LateUpdate,
        FixedUpdate,
        Destroy,
        LateDestroy,
        ApplicationQuit,
        LateApplicationQuit,
    }
}