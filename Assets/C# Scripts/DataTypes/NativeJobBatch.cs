using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;


/// <summary>
/// Batch container type that allows live read writes to a NativeArray of type <typeparamref name="T"/> and syncs it to a Job only copy every <see cref="UpdateJobBatch"/> call
/// </summary>
public class NativeJobBatch<T> where T : unmanaged
{
    public NativeArray<T> NextBatch;
    public int NextBatchCount;

    public NativeArray<T> JobBatch;
    public int JobBatchCount;

    private readonly Allocator allocator;


    public T this[int index]
    {
        get => NextBatch[index];
        set => NextBatch[index] = value;
    }

    public NativeJobBatch(int startBatchSize, Allocator allocator = Allocator.Persistent)
    {
        JobBatch = new NativeArray<T>(startBatchSize, allocator);
        NextBatch = new NativeArray<T>(startBatchSize, allocator);

        this.allocator = allocator;
    }

    public unsafe void Add(T toAdd)
    {
        if (NextBatchCount >= NextBatch.Length)
        {
            int newLength = math.max(NextBatch.Length * 2, 1);

            NativeArray<T> newArray = new NativeArray<T>(newLength, allocator, NativeArrayOptions.UninitializedMemory);

            UnsafeUtility.MemCpy(
                newArray.GetUnsafePtr(),
                NextBatch.GetUnsafeReadOnlyPtr(),
                NextBatchCount * UnsafeUtility.SizeOf<T>());

            NextBatch.Dispose();
            NextBatch = newArray;
        }
        NextBatch[NextBatchCount++] = toAdd;
    }
    public void RemoveAtSwapBack(int id)
    {
        NextBatchCount--;

        if (id == NextBatchCount) return;

        // Intentional non clear of last entry, it doesnt matter.
        NextBatch[id] = NextBatch[NextBatchCount];
    }
    public void RemoveLastEntry()
    {
        // Intentional non clear of last entry, it doesnt matter.
        NextBatchCount--;
    }

    public unsafe void UpdateJobBatch()
    {
        // Ensure CurrentBatch can hold NextBatch
        if (JobBatch.Length < NextBatchCount)
        {
            NativeArray<T> newArray = new NativeArray<T>(NextBatchCount, allocator, NativeArrayOptions.UninitializedMemory);

            JobBatch.Dispose();
            JobBatch = newArray;
        }

        JobBatchCount = NextBatchCount;
        UnsafeUtility.MemCpy(
            JobBatch.GetUnsafePtr(),
            NextBatch.GetUnsafeReadOnlyPtr(),
            NextBatchCount * UnsafeUtility.SizeOf<T>());
    }

    public void Dispose()
    {
        JobBatch.DisposeIfCreated();
        NextBatch.DisposeIfCreated();
    }
}
