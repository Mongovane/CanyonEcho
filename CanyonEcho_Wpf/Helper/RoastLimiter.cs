using System;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// Roast 限流器 (v4.3.0 新增)。
    /// 问题: 之前 OCR 每识别一次就调一次 LLM/内置引擎, 热键防抖只有 300ms,
    /// 连按 F5 或者对面刷屏, 一分钟能往游戏里发十几句 roast, 烧 API 额度不说,
    /// Riot 的举报/禁言就是按这个算的, 一局喷太多等于自首。
    /// 规则 (默认值, AiConfig 可调):
    /// - 冷却: 两次自动 roast 之间至少 RoastCooldownSeconds 秒 (默认 5 秒);
    /// - 上限: 一局 (按 10 分钟滑动窗口算, 没有对局结束事件, 这是最接近的办法)
    ///   最多 RoastMaxPerMatch 条 (默认 12 条), 超了就不再自动发, 只在界面上提示。
    /// 手动 F2/F3/F6 词库发送不走这个限流, 只限 OCR 自动反击。
    /// 线程安全: 所有状态都在 lock 里改, OCR 回调和 UI 线程同时调也没事。
    /// </summary>
    public class RoastLimiter
    {
        private readonly object _lock = new object();
        private DateTime _lastRoastTime = DateTime.MinValue;
        private DateTime _windowStart = DateTime.Now;
        private int _countInWindow;

        public int CooldownSeconds { get; set; } = 5;
        public int MaxPerMatch { get; set; } = 12;

        /// <summary>窗口长度, 一局按 10 分钟算。超了就清零重算。</summary>
        public int WindowMinutes { get; set; } = 10;

        public int CountInWindow { get { lock (_lock) { return _countInWindow; } } }

        /// <summary>
        /// 这次自动 roast 能不能发。返回 false 时 out 参数说明原因,
        /// 调用方不要发, 只把原因显示在 LastAiReply 里。
        /// </summary>
        public bool TryAllowRoast(DateTime now, out string denyReason)
        {
            lock (_lock)
            {
                if ((now - _windowStart).TotalMinutes >= WindowMinutes)
                {
                    _windowStart = now;
                    _countInWindow = 0;
                }

                if (_countInWindow >= MaxPerMatch)
                {
                    denyReason = $"本局 roast 已发 {_countInWindow} 条 (上限 {MaxPerMatch} 条), 已自动停发, 免得被举报禁言";
                    return false;
                }

                double sinceLast = (now - _lastRoastTime).TotalSeconds;
                if (sinceLast < CooldownSeconds)
                {
                    denyReason = $"roast 冷却中, 还剩 {Math.Ceiling(CooldownSeconds - sinceLast)} 秒";
                    return false;
                }

                _countInWindow++;
                _lastRoastTime = now;
                denyReason = null;
                return true;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _countInWindow = 0;
                _windowStart = DateTime.Now;
                _lastRoastTime = DateTime.MinValue;
            }
        }
    }
}
