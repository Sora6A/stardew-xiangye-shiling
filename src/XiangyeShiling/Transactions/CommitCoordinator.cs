namespace XiangyeShiling.Transactions;

/// <summary>适配器预先构造的写集。创建阶段不得改动游戏；回滚保留原物品与原状态。</summary>
public interface IPreparedCommit
{
    bool MatchesOriginal();
    void ApplyInventory();
    void ApplyState();
    bool IsApplied();
    void RestoreInventory();
    void RestoreState();
    bool IsRestored();
}

public enum CommitResult { Applied, Conflict, Reentrant, PreparationFailed, RolledBack, RecoveryFailed, Quarantined }

/// <summary>
/// 单人主线程提交边界：准备写集 → 最后核对 → 写库存/状态；抛错则分别恢复两侧。
/// 不是分布式事务协议。回滚不能恢复时隔离本会话，不继续产生第二笔变化。
/// </summary>
public sealed class CommitCoordinator
{
    private bool InProgress;
    public bool IsQuarantined { get; private set; }

    public CommitResult Commit(Func<IPreparedCommit> prepare, Action<Exception> report)
    {
        if (this.IsQuarantined) return CommitResult.Quarantined;
        if (this.InProgress) return CommitResult.Reentrant;
        this.InProgress = true;
        try
        {
            IPreparedCommit target;
            try
            {
                target = prepare();
                if (!target.MatchesOriginal()) return CommitResult.Conflict;
            }
            catch (Exception ex)
            {
                Report(ex);
                return CommitResult.PreparationFailed;
            }
            try
            {
                target.ApplyInventory();
                target.ApplyState();
                // BUG-2026-003: 同步回调可能静默改写刚提交的数据；返回成功前必须核对最终写集。
                if (!target.IsApplied()) throw new InvalidOperationException("Committed values don't match the prepared transaction.");
                return CommitResult.Applied;
            }
            catch (Exception ex)
            {
                Report(ex);
                // A setter may throw AFTER its side effect. Always attempt both compensations.
                try { target.RestoreInventory(); } catch (Exception recovery) { Report(recovery); }
                try { target.RestoreState(); } catch (Exception recovery) { Report(recovery); }
                try
                {
                    if (target.IsRestored()) return CommitResult.RolledBack;
                }
                catch (Exception recovery) { Report(recovery); }
                this.IsQuarantined = true;
                return CommitResult.RecoveryFailed;
            }
        }
        finally { this.InProgress = false; }

        void Report(Exception error)
        {
            // A logging callback must never interrupt compensation.
            try { report(error); } catch { /* Recovery must still run. */ }
        }
    }
}
