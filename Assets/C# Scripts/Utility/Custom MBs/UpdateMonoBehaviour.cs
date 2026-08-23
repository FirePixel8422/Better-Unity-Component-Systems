using UnityEngine;
using Fire_Pixel.Utility;


public class UpdateMonoBehaviour : MonoBehaviour
{
    protected virtual void OnEnable()
    {
        CallbackScheduler.RegisterCallback(CallbackType.Update, OnUpdate);
    }
    protected virtual void OnDisable()
    {
        CallbackScheduler.UnRegisterCallback(CallbackType.Update, OnUpdate);
    }
    /// <summary>
    /// Called every frame.
    /// </summary>
    protected virtual void OnUpdate() { }
}