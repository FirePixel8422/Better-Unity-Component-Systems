using Fire_Pixel.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;


public static class ExtensionMethods
{
    #region Invoke

    /// <summary>
    /// Invoke function <paramref name="f"/> after <paramref name="delay"/> seconds. Schedules a coroutine on the target <see cref="MonoBehaviour"/>
    /// </summary>
    /// <returns>The scheduled coroutine ref</returns>
    public static InvokeCallbackReference Invoke(this MonoBehaviour mb, float delay, Action f)
    {
        return CallbackScheduler.Invoke(delay, f, mb.GetInstanceID());
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

    #endregion


    #region Transform Logic

    public static void SetParent(this Transform trans, Transform parent, bool keepLocalPos, bool keepLocalRot)
    {
        if (parent == null)
        {
#if UNITY_EDITOR
            DebugLogger.LogWarning("You are trying to set a transform to a parent that doesnt exist, this is not allowed");
#endif
            return;
        }

        trans.SetParent(parent);
        if (!keepLocalPos)
        {
            trans.localPosition = Vector3.zero;
        }
        if (!keepLocalRot)
        {
            trans.localRotation = Quaternion.identity;
        }
    }
    public static void SetParent(this Transform trans, Transform parent, bool keepLocalPos, bool keepLocalRot, bool keepLocalScale)
    {
        if (parent == null)
        {
#if UNITY_EDITOR
            DebugLogger.LogWarning("You are trying to set a transform to a parent that doesnt exist, this is not allowed");
#endif
            return;
        }

        trans.SetParent(parent);
        if (!keepLocalPos)
        {
            trans.localPosition = Vector3.zero;
        }
        if (!keepLocalRot)
        {
            trans.localRotation = Quaternion.identity;
        }
        if (!keepLocalScale)
        {
            trans.localScale = Vector3.one;
        }
    }

    #endregion


    #region (Try)GetComponent(s)

    public static bool TryGetComponents<T>(this Transform trans, out T[] components) where T : UnityEngine.Object
    {
        components = trans.GetComponents<T>();

        return components.Length > 0;
    }

    public static bool TryGetComponentInChildren<T>(this Transform trans, out T component, bool includeInactive = false) where T : UnityEngine.Object
    {
        component = trans.GetComponentInChildren<T>(includeInactive);
        return component != null;
    }
    public static bool TryGetComponentsInChildren<T>(this Transform trans, out T[] components, bool includeInactive) where T : UnityEngine.Object
    {
        components = trans.GetComponentsInChildren<T>(includeInactive);

        return components.Length > 0;
    }

    public static bool TryGetComponentInParent<T>(this Transform trans, out T component) where T : UnityEngine.Object
    {
        component = trans.GetComponentInParent<T>();
        return component != null;
    }
    public static bool TryGetComponentsInParent<T>(this Transform trans, out T[] component) where T : UnityEngine.Object
    {
        component = trans.GetComponentsInParent<T>();
        return component != null;
    }

    public static bool TryGetComponentInParentRecursively<T>(this Transform trans, bool checkStartTransform, out T component) where T : UnityEngine.Object
    {
        Transform current = checkStartTransform ? trans : trans.parent;

        while (current != null)
        {
            if (current.TryGetComponent(out component))
            {
                return true;
            }

            current = current.parent;
        }

        component = null;
        return false;
    }

    #endregion


    #region (Try)FindObjectOfType

    public static bool TryFindObjectOfType<T>(this UnityEngine.Object obj, out T component, bool includeInactive = false) where T : UnityEngine.Object
    {
        FindObjectsInactive findObjectsInactive = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;

        component = UnityEngine.Object.FindFirstObjectByType<T>(findObjectsInactive);
        return component != null;
    }
    public static bool TryFindObjectsOfType<T>(this UnityEngine.Object obj, out T[] component, bool includeInactive = false, bool sortByInstanceID = false) where T : UnityEngine.Object
    {
        FindObjectsInactive findObjectsInactive = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        FindObjectsSortMode sortMode = sortByInstanceID ? FindObjectsSortMode.InstanceID : FindObjectsSortMode.None;

        component = UnityEngine.Object.FindObjectsByType<T>(findObjectsInactive, sortMode);
        return component != null;
    }

    // Unity 6s new FindobjectOfType is stupid
    public static T FindObjectOfType<T>(this UnityEngine.Object obj) where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindFirstObjectByType<T>();
    }

    public static T[] FindObjectsOfType<T>(this UnityEngine.Object obj, bool includeInactive = false, bool sortByInstanceID = false) where T : UnityEngine.Object
    {
        FindObjectsInactive findObjectsInactive = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        FindObjectsSortMode sortMode = sortByInstanceID ? FindObjectsSortMode.InstanceID : FindObjectsSortMode.None;

        return UnityEngine.Object.FindObjectsByType<T>(findObjectsInactive, sortMode);
    }

    #endregion


    #region GetComponents Ordered

    public static T[] GetComponentsOrdered<T>(this Component comp) where T : Component
    {
        T[] components = comp.GetComponents<T>();

        System.Array.Sort(components, (a, b) =>
        {
            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        });

        return components;
    }

    public static T[] GetComponentsInChildrenOrdered<T>(this Transform trans, bool includeInactive = false) where T : Component
    {
        T[] components = trans.GetComponentsInChildren<T>(includeInactive);

        System.Array.Sort(components, (a, b) =>
        {
            string pathA = GetHierarchyPath(a.transform);
            string pathB = GetHierarchyPath(b.transform);
            return string.CompareOrdinal(pathA, pathB);
        });

        return components;
    }

    public static T[] GetComponentsInParentOrdered<T>(this Transform trans, bool includeInactive = false) where T : Component
    {
        T[] components = trans.GetComponentsInParent<T>(includeInactive);

        System.Array.Sort(components, (a, b) =>
        {
            string pathA = GetHierarchyPath(a.transform);
            string pathB = GetHierarchyPath(b.transform);
            return string.CompareOrdinal(pathA, pathB);
        });

        return components;
    }

    public static T[] FindObjectsOfTypeOrdered<T>() where T : Component
    {
#pragma warning disable CS0618
        T[] objects = UnityEngine.Object.FindObjectsOfType<T>();
#pragma warning restore CS0618

        System.Array.Sort(objects, (a, b) =>
        {
            string pathA = GetHierarchyPath(a.transform);
            string pathB = GetHierarchyPath(b.transform);
            return string.CompareOrdinal(pathA, pathB);
        });

        return objects;
    }

    private static string GetHierarchyPath(Transform t)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder(64);

        while (t != null)
        {
            builder.Insert(0, '/');
            builder.Insert(1, t.name);
            t = t.parent;
        }

        return builder.ToString();
    }

    #endregion


    #region HasComponent

    public static bool HasComponent<T>(this Transform trans) where T : Component
    {
        return trans.GetComponent<T>() != null;
    }

    public static bool HasComponentInChildren<T>(this Transform trans, bool includeInactive = false) where T : Component
    {
        return trans.GetComponentInChildren<T>(includeInactive) != null;
    }

    public static bool HasComponentInParent<T>(this Transform trans, bool includeInactive = false) where T : Component
    {
        return trans.GetComponentInParent<T>(includeInactive) != null;
    }

    #endregion


    #region PlayClip with Pitch and Clip overloads for AudioSource

    /// <summary>
    /// Lets AudioSource play selected clip with selected pitch
    /// </summary>
    public static void PlayClipWithPitch(this AudioSource source, AudioClip clip, float pitch, float volume = -1)
    {
        source.clip = clip;
        source.pitch = pitch;
        if (volume != -1)
        {
            source.volume = volume;
        }
        source.Play();
    }
    /// <summary>
    /// Lets AudioSource play with selected pitch
    /// </summary>
    public static void PlayWithPitch(this AudioSource source, float pitch, float volume = -1)
    {
        source.pitch = pitch;
        if (volume != -1)
        {
            source.volume = volume;
        }
        source.Play();
    }
    /// <summary>
    /// Lets AudioSource play with selected pitch
    /// </summary>
    public static void PlayOneShotWithPitch(this AudioSource source, float pitch, float volume = -1)
    {
        source.pitch = pitch;
        if (volume != -1)
        {
            source.volume = volume;
        }
        source.PlayOneShot(source.clip);
    }
    /// <summary>
    /// Lets AudioSource play selected clip with selected pitch
    /// </summary>
    public static void PlayOneShotClipWithPitch(this AudioSource source, AudioClip clip, float pitch, float volume = -1)
    {
        source.pitch = pitch;
        if (volume != -1)
        {
            source.volume = volume;
        }
        source.PlayOneShot(clip);
    }

    #endregion


    #region SetActiveSmart for GameObjects and Components

    /// <summary>
    /// Set the active state of a Behaviour component only if the state is different from the current state.
    /// </summary>
    public static void SetActiveStateSmart(this Behaviour comp, bool state)
    {
        if (comp.enabled != state)
        {
            comp.enabled = state;
        }
    }

    /// <summary>
    /// Set the active state of a GameObject only if the state is different from the current state.
    /// </summary>
    public static void SetActiveSmart(this GameObject obj, bool state)
    {
        if (obj.activeInHierarchy != state)
        {
            obj.SetActive(state);
        }
    }

    #endregion


    #region DisposeIfCreated for Native Collections

    /// <summary>
    /// Check if the NativeArray is created, and if so, dispose of it
    /// </summary>
    public static void DisposeIfCreated<T>(this NativeArray<T> array) where T : unmanaged
    {
        if (array.IsCreated)
            array.Dispose();
    }
    /// <summary>
    /// Check if the NativeArray is created, and if so, dispose of it
    /// </summary>
    public static void DisposeIfCreated<T>(this NativeList<T> array) where T : unmanaged
    {
        if (array.IsCreated)
            array.Dispose();
    }
    /// <summary>
    /// Check if the NativeArray is created, and if so, dispose of it
    /// </summary>
    public static void DisposeIfCreated<T>(this NativeReference<T> array) where T : unmanaged
    {
        if (array.IsCreated)
            array.Dispose();
    }
    /// <summary>
    /// Check if the NativeArray is created, and if so, dispose of it
    /// </summary>
    public static void DisposeIfCreated<T>(this NativeHashSet<T> array) where T : unmanaged, IEquatable<T>
    {
        if (array.IsCreated)
            array.Dispose();
    }
    /// <summary>
    /// Check if the NativeArray is created, and if so, dispose of it
    /// </summary>
    public static void DisposeIfCreated<Tkey, TValue>(this NativeHashMap<Tkey, TValue> array) where Tkey : unmanaged, IEquatable<Tkey> where TValue : unmanaged
    {
        if (array.IsCreated)
            array.Dispose();
    }

    #endregion


    #region Array/List/NativeCollection Shuffle

    /// <summary>
    /// Randomly shuffles the content of the array in place using Fisher–Yates.
    /// </summary>
    public static void Shuffle<T>(this T[] targetArray)
    {
        int n = targetArray.Length;
        for (int i = 0; i < n - 1; i++)
        {
            int j = UnityEngine.Random.Range(i, n);
            (targetArray[i], targetArray[j]) = (targetArray[j], targetArray[i]);
        }
    }
    /// <summary>
    /// Randomly shuffles the content of the array in place using Fisher–Yates.
    /// </summary>
    public static void Shuffle<T>(this List<T> targetArray)
    {
        int n = targetArray.Count;
        for (int i = 0; i < n - 1; i++)
        {
            int j = UnityEngine.Random.Range(i, n);
            (targetArray[i], targetArray[j]) = (targetArray[j], targetArray[i]);
        }
    }
    /// <summary>
    /// Randomly shuffles the content of the array in place using Fisher–Yates.
    /// </summary>
    public static void Shuffle<T>(this NativeArray<T> targetArray) where T : unmanaged
    {
        int n = targetArray.Length;
        for (int i = 0; i < n - 1; i++)
        {
            int j = UnityEngine.Random.Range(i, n);
            (targetArray[i], targetArray[j]) = (targetArray[j], targetArray[i]);
        }
    }
    /// <summary>
    /// Randomly shuffles the content of the array in place using Fisher–Yates.
    /// </summary>
    public static void Shuffle<T>(this NativeList<T> targetArray) where T : unmanaged
    {
        int n = targetArray.Length;
        for (int i = 0; i < n - 1; i++)
        {
            int j = UnityEngine.Random.Range(i, n);
            (targetArray[i], targetArray[j]) = (targetArray[j], targetArray[i]);
        }
    }

    #endregion


    #region Index Modify Utility

    /// <summary>
    /// Increments index by 1 and wraps to 0 if it reaches length
    /// </summary>
    public static int IncrementSmart(this ref int value, int length)
    {
        value += 1;
        if (value >= length)
        {
            value = 0;
        }
        return value;
    }
    /// <summary>
    /// Decrements index by 1 and wraps to length if it reaches 0
    /// </summary>
    public static int DecrementSmart(this ref int value, int length)
    {
        value -= 1;
        if (value < 0)
        {
            value = length - 1;
        }
        return value;
    }

    /// <summary>
    /// Updates index by adding <paramref name="toAdd"/> and wraps to 0 if it reaches length or length if it reaches 0
    /// </summary>
    public static int AddSmart(this ref int value, int toAdd, int length)
    {
        DebugLogger.LogError("AddSmart called with length 0", length <= 0);

        value += toAdd;
        while (value >= length)
        {
            value -= length;
        }
        while (value < 0)
        {
            value += length;
        }
        return value;
    }

    #endregion


    #region Array Utility

    /// <summary>
    /// Selects and returns a random entry of array.
    /// </summary>
    public static T SelectRandom<T>(this T[] targetArray)
    {
        return targetArray[UnityEngine.Random.Range(0, targetArray.Length)];
    }

    /// <summary>
    /// Selects and returns a random array filled with entries of <paramref name="targetArray"/>
    /// </summary>
    public static T[] SelectRandomRange<T>(this T[] targetArray, int entryCount, bool forceUniqueEntries = true)
    {
        T[] output = new T[entryCount];

        if (forceUniqueEntries)
        {
            List<int> numberPot = new List<int>(entryCount);
            for (int i = 0; i < targetArray.Length; i++)
            {
                numberPot.Add(i);
            }

            int r;
            for (int i = 0; i < entryCount; i++)
            {
                r = UnityEngine.Random.Range(0, numberPot.Count);

                output[i] = targetArray[numberPot[r]];

                numberPot.RemoveAt(r);
            }
        }
        else
        {
            int r;
            for (int i = 0; i < entryCount; i++)
            {
                r = UnityEngine.Random.Range(0, entryCount);

                output[i] = targetArray[r];
            }
        }
        return output;
    }

    /// <summary>
    /// Modifies a struct element in a list safely by copying, running the modifier, then writing it back.
    /// </summary>
    public static void ModifyAt<T>(this List<T> list, int index, ActionRef<T> modifier) where T : struct
    {
        T copy = list[index];
        modifier(ref copy);
        list[index] = copy;
    }
    public delegate void ActionRef<T>(ref T value);


    /// <returns>Wheather array is valid and has at least 1 entry</returns>
    public static bool HasData<T>(this T[] array)
    {
        return array != null && array.Length > 0;
    }
    /// <returns>Wheather array is invalid or its length is 0</returns>
    public static bool IsNullOrEmpty<T>(this T[] array)
    {
        return array == null || array.Length == 0;
    }
    /// <returns>Wheather array is invalid or its length is 0</returns>
    public static bool IsNotNullOrEmpty<T>(this T[] array)
    {
        return array != null && array.Length != 0;
    }
    /// <returns>Wheather array is invalid or its length is 0 or it has null at one of its entries</returns>
    public static bool HasInvalidData<T>(this T[] array)
    {
        bool arrayValid = array != null && array.Length > 0;
        if (arrayValid)
        {
            int count = array.Length;
            for (int i = 0; i < count; i++)
            {
                if (array[i] == null)
                {
                    return true;
                }
            }
        }
        return false;
    }
    /// <returns>Wheather array is valid or its length is more then 0 and it has no null at none of its entries</returns>
    public static bool HasNoInvalidData<T>(this T[] array)
    {
        bool arrayValid = array != null && array.Length > 0;
        if (arrayValid)
        {
            int count = array.Length;
            for (int i = 0; i < count; i++)
            {
                if (array[i] == null)
                {
                    return false;
                }
            }
        }
        return true;
    }

    #endregion


    /// <summary>
    /// Try finding an action by name, returns true if found, false if not. Outputs the found action
    /// </summary>
    public static bool TryFindAction(this InputActionAsset actionAsset, string actionName, out InputAction action)
    {
        action = actionAsset.FindAction(actionName);
        return action != null;
    }

    /// <summary>
    /// This method uses reflection and calls <see cref="Type.GetInterfaces"/>.
    /// Avoid calling repeatedly in performance-critical code paths.
    /// Cache the result if this is used frequently.
    /// </summary>
    /// <returns>True if <paramref name="type"/> implements <paramref name="interfaceType"/></returns>
    public static bool ImplementsInterface(this Type type, Type interfaceType)
    {
        return type.GetInterfaces().Any(x =>
            x.IsGenericType
                ? x.GetGenericTypeDefinition() == interfaceType
                : x == interfaceType);
    }
}