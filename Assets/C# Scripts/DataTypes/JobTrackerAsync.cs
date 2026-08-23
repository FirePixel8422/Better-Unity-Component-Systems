using Fire_Pixel.Utility;
using System;
using Unity.Jobs;


/// <summary>
/// Tracks a <see cref="JobHandle"/> and automatically completes it once the job finishes.
/// Invokes the completion callback after the job has been completed.
/// </summary>
public class JobTrackerAsync
{
    private Action onComplete;
    private JobHandle handle;
    private bool isJobActive;
    public bool IsJobActive => isJobActive;


    private JobTrackerAsync() { }
    public JobTrackerAsync(Action onCompleteAction)
    {
        onComplete = onCompleteAction;
        CallbackScheduler.RegisterCallback(CallbackType.Update, OnUpdate);
    }
    /// <summary>
    /// Completes the currently tracked job, if any, and unregisters this tracker from the update callback.
    /// </summary>
    public void Dispose()
    {
        if (isJobActive)
        {
            handle.Complete();
            isJobActive = false;
        }

        onComplete = null;
        CallbackScheduler.UnRegisterCallback(CallbackType.Update, OnUpdate);
    }

    /// <summary>
    /// (Re-)Register a JobHandle to track for completion and invoke <see cref="onComplete"/> on its completion.
    /// </summary>
    public void TrackJobHandle(JobHandle newHandle)
    {
        handle = newHandle;
        isJobActive = true;
    }

    private void OnUpdate()
    {
        if (!isJobActive || !handle.IsCompleted) return;

        handle.Complete();
        isJobActive = false;

        onComplete?.Invoke();
    }
}