namespace XiangyeShiling.Menus;

/// <summary>把已保存的绝对分钟换成玩家能看到的最早领取时刻。仅用于显示。</summary>
internal readonly record struct BatchCompletion(long DaysAhead, int Clock)
{
    public bool IsTonight => this.DaysAhead == 1 && this.Clock < 200;

    public static BatchCompletion FromDeadline(long readyAt, int currentDay)
    {
        const int minutesPerDay = 24 * 60;
        const int tickMinutes = 10;
        // 在日内取整，既能处理不落在十分钟刻度上的存档，也不必对绝对截止时间做溢出加法。
        int minute = (int)(readyAt % minutesPerDay);
        int nextTick = (minute + tickMinutes - 1) / tickMinutes * tickMinutes;
        long day = readyAt / minutesPerDay + nextTick / minutesPerDay;
        minute = nextTick % minutesPerDay;
        // 02:00 到 06:00 期间无法领取，最早在次日醒来后操作。
        if (minute is >= 2 * 60 and < 6 * 60) minute = 6 * 60;
        return new(day - currentDay, minute / 60 * 100 + minute % 60);
    }
}
