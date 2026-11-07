using System;
using System.Collections.Generic;
using System.Threading;

namespace XiangyeShiling.Networking;

/// <summary>
/// 将任意接收线程的工作交给构造线程消费的有界 FIFO 邮箱；构造者应为游戏主线程适配器。
/// 满队列拒绝新项，保留已经排队的内容；排队本身不执行回调，不认证发送者，也不提交设备事务。
/// </summary>
/// <remarks>
/// P1-NET-02：传输端在读取/解析正文前限额，消费时核验当前生命周期及连接元数据。
/// 未来写入仍须额外核验可信身份/实际库存与状态；邮箱不提供这些保证。
/// 容量限制只约束排队项数量，不限制每项内存或网络输入速率，不能作为完整的防拒绝服务方案。
/// Clear 只移除调用时已经排队的项；生命周期结束还须先停止旧生产者，或拒绝其过期会话，
/// 防止清空之后的旧回调再次入队。ReadOnlyProtocolService 用生命周期 generation 再次核验。
/// </remarks>
public sealed class ProtocolInbox<T> where T : class
{
    private readonly object Sync = new();
    private readonly Queue<T> Items = new();
    private readonly int Capacity;
    private readonly Thread OwnerThread;

    public ProtocolInbox(int capacity = 128)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Inbox capacity must be positive.");
        this.Capacity = capacity;
        this.OwnerThread = Thread.CurrentThread;
    }

    /// <summary>任意线程可读取同一锁保护的瞬时数量；不能用该快照代替 TryEnqueue 的原子容量判断。</summary>
    public int Count
    {
        get { lock (this.Sync) return this.Items.Count; }
    }

    /// <summary>
    /// 任意线程可入队；空项属于调用错误并抛 ArgumentNullException，满队列返回 false 且不改变内容。
    /// 容量判断与入队持有同一把锁，因此并发生产者不能突破上限。
    /// </summary>
    public bool TryEnqueue(T item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));
        lock (this.Sync)
        {
            if (this.Items.Count >= this.Capacity)
                return false;
            this.Items.Enqueue(item);
            return true;
        }
    }

    /// <summary>
    /// 仅构造线程可取出一个原引用；空邮箱返回 false/null，其他线程在接触队列前抛 InvalidOperationException。
    /// 调用者在返回后执行工作，邮箱不在持锁期间调用用户代码或推进游戏状态。
    /// </summary>
    public bool TryDequeue(out T? item)
    {
        this.RequireOwnerThread();
        lock (this.Sync)
        {
            if (this.Items.Count == 0)
            {
                item = null;
                return false;
            }
            item = this.Items.Dequeue();
            return true;
        }
    }

    /// <summary>
    /// 仅构造线程可原子清除待处理项，释放旧生命周期队列引用；不执行、保存或取消回调。
    /// 随后仍可入队，故停止旧生产者/校验会话由生命周期适配器负责。
    /// </summary>
    public void Clear()
    {
        this.RequireOwnerThread();
        lock (this.Sync) this.Items.Clear();
    }

    private void RequireOwnerThread()
    {
        if (!ReferenceEquals(Thread.CurrentThread, this.OwnerThread))
            throw new InvalidOperationException("Only the inbox construction thread may consume or clear it.");
    }
}
