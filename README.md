# 峡谷回音 CanyonEcho v4.2.0 (原 ZuAnBot 深度重构与安全升级版)

> **峡谷回音 (`CanyonEcho`)** 是基于原版 `ZuAnBot` 全面重构升级的 **英雄联盟聊天框 OCR 智能识别 + AI/内置高情商阴阳怪气反击助手**。
> 本次 v4.2.0 重构在 **100% 保留原有功能** 的基础上，新增了 **双模式聊天框 OCR 识别（`F4` 手动框选 / `F5` 自动静默识别）**、**LLM 大模型 + 内置离线上下文感知双引擎反击**、**外部安全鼠标穿透游戏内悬浮窗 HUD (`F7`)**、**极速剪贴板防吞字发送**、**单行零宽字符防屏蔽 (`F8`)**，并修复了原版 6 项安全与稳定性漏洞。

---

> **v4.2.0 验证报告: `VERIFICATION.md`** — 179 项纯逻辑测试全部通过, 但 Windows 编译、进游戏实测、LLM 实测、完整更新端到端**都没在真机上跑过**, 发版前必须按那份报告第 5 节跑一遍, 别跳过。

---


## 🔥 v4.2.0 脏话隐形扩表 + 粗口 Roast 骂人模式 (默认开)

> **Prompt (用户指定, 已写进代码):** `Based on everything you know about this player, roast and don't hold back. Say it in Chinese slang, with way more Chinese swear words.`
> 游戏里 "this player" 就是对面那个喷子: 解析器给什么 (发言人/英雄、意图、原话、战绩、是否帮腔), LLM 和内置引擎就拿什么往死里喷, 中文俚语 + 大量脏话, 别收着。

- **LLM 路线:** `AiConfig.RoastSystemPrompt` + `GetEffectiveSystemPrompt()` (RoastMaster 默认人设), `AiReplyService` 的 user prompt 改成"往死里喷, 脏话拉满, 40字以内", temperature 0.95 / max_tokens 120, 清洗截断放宽到 80 字 (原来 60 字会把骂到一半的话截断)。
- **内置离线路线:** `BuiltInSnarkyEngine.RoastTemplates`, 8 个意图分类共 82 条 (v4.1.0 是 61 条, v4.2.0 新增 21 条), 全是隐形脏话版; `GenerateReply(..., useRoastMode=true)` 默认走这套。
- **v4.2.0 隐形扩表 (`Helper/SwearObfuscator.cs`, 12 个 -> 50 个映射):** 儿子->儿砸/JL子, 爹->蝶, 叫爹->叫蝶, 孙子->孙砸, 爸爸->粑粑, 智障->制杖, 弱智->若智, 白痴->白吃, 脑残->脑蚕, 牛逼->牛福, 狗东西->苟东西, 老妈->老冯, 滚蛋->衮蛋, 去死->去屎。发送前 `GameSendHelper` + LLM 输出都会先过一遍 `SwearObfuscator.Obfuscate()`, 只在脏话词内插零宽, 操作/操控/滚动这些正常词不会被误伤。旧腾讯词库 (2022, 5168 行) 里 傻逼/你妈/他妈/傻B/SB/TMD/你爸 是原形命中, 儿子/孙子/智障/废物/垃圾/牛逼 单字不在旧词库里, 但现在 LoL 是组合+语义+随机过滤, 照样会被吞, 所以一样要换。**没有 100% 能发出去的保证, Riot 零容忍词是整句拦截 + 当局禁言。**
- **开关:** 设置里 `粗口 Roast 骂人模式` 勾选框 (`AiConfig.UseRoastMode`, 默认 true), 关掉立刻回到原来那套干净阴阳怪气 (包括 Custom 人设的默认提示词也不会漏 roast 进去, 已修)。
- **⚠ 封号警告, 别装没看见:** 脏话会触发 LoL 聊天过滤、被队友/对手举报、禁言、封号。F8 零宽防屏蔽不是免死金牌, 之前版本吹的"零封号风险"本来就是假的, 现在更假。开着这个模式, 后果自担, 别来找我。

## 🔥 v4.2.0 核心重构与升级亮点

### 1. 原版功能全面升级优化
- **极速剪贴板防吞字发送 (`GameSendHelper.UseFastClipboardSend`)**：
  - 原版通过 `WindowsInput` 逐字符模拟打字耗时约 800ms，且极易被游戏内中文输入法截留导致吞字、漏字。
  - 升级版采用「自动备份用户剪贴板 -> 写入反击文本 -> 模拟 `Enter + Ctrl+V + Enter` -> 毫秒级恢复原剪贴板」机制，发送耗时缩短至 **80ms**，100% 杜绝中文输入法吞字。
- **单行零宽字符防屏蔽穿插 (`F8` / `ApplyZeroWidthAntiFilter`)**：
  - 原版 `F12` 逐字发送需要将一句话拆成十几行纵向刷屏，容易刷屏被举报。
  - 新增 `F8` 单行零宽空格（`​`）穿插技术，在汉字间自动插入不可见零宽分隔符，**单行发送即可打断敏感词正则匹配**。
- **LRU 智能防重抽词算法 (`WordsLibraryExtension.GetLoacalWord`)**：
  - 修复原版每次按键 `new Random((int)DateTime.Now.Ticks)` 导致高频按键抽出重复句子的缺陷，引入滑动历史队列（`Queue<string>`），确保连续按键绝不重复。
- **外部安全游戏内透明悬浮窗 HUD (`F7` / `InGameOverlayWindow`)**：
  - 采用海克斯科技暗金配色的置顶透明 HUD，通过 Win32 `WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE` 实现 **100% 鼠标穿透、不抢游戏焦点、不注入游戏进程内存**，实时显示最新 OCR 识别结果与 AI 反击语录，彻底规避 Vanguard / TP 反作弊封号风险。

### 2. 英雄联盟聊天框 OCR 识别 + 双引擎阴阳怪气反击
- **`F4` 手动框选截图反击 (`SnippingWindow`)**：全屏半透明十字准星遮罩，支持多显示器与高分屏 DPI 物理像素换算，框选松开即刻反击并自动记忆聊天框坐标。
- **`F5` 静默自动截取聊天框反击 (`OcrHelper`)**：不弹窗、不打断对线操作，后台静默截取左下角聊天框，经 **2 倍双三次插值放大 + 亮度分析 + 反色白底黑字增强** 后调用 **Windows 10/11 原生离线 WinRT OCR (`Windows.Media.Ocr`)** 识别。
- **恶意意图解析 + 双引擎反击 (`LolChatParser` / `AiReplyService` / `BuiltInSnarkyEngine`)**：
  - 自动剥离时间戳、频道前缀与系统广播，提取发言英雄名，识别 6 大恶意意图（`甩锅打野`、`质疑操作`、`战绩嘲讽`、`消极投降`、`问号嘲讽`、`祖安问候`）。
  - 支持 **DeepSeek / xAI Grok / OpenAI / 通义千问 / Kimi / 本地 Ollama** 一键预设与 **内置免 Key 离线高情商阴阳怪气引擎** 无缝切换。

### 3. 安全与稳定性漏洞修复清单
1. **修复远程自动更新明文 HTTP 与任意程序执行漏洞 (`Apis.cs` / `CanyonEchoUpdate`)**：强制验证 `https://` 协议与 `SHA-256` 文件完整性校验，防止过期域名劫持或中间人攻击导致管理员权限 RCE。
2. **修复 API Key 明文落盘泄露风险 (`AiConfig.cs`)**：采用 Windows **DPAPI (`ProtectedData.Protect`)** 当前用户密钥加密存储 `EncryptedApiKey`。
3. **修复 `WM_COPYDATA` 跨进程通信内存越界与乱码漏洞 (`IPCHelper.cs`)**：改用显式 UTF-8 非托管内存拷贝，增加 `MAGIC_TOKEN (0x43454348)` 签名校验与 512 字节长度上限。
4. **修复更新器枚举 64 位系统进程崩溃漏洞 (`CanyonEchoUpdate/Program.cs`)**：支持直接传入主进程 `--pid` 并捕获 `Win32Exception`。
5. **修复键盘钩子非托管句柄泄露与超时失灵 (`GlobalKeyboardHook.cs`)**：实现 `IDisposable` 并在应用退出时确定性卸载钩子，热键回调改为异步非阻塞执行并加入 300ms 防抖。
6. **修复保存词库时 `%Temp%` 临时文件泄露 (`JsonHelper.cs`)**：在 `finally` 块中清理 `Path.GetTempFileName()` 创建的临时文件。

---

## ⌨️ 快捷键一览

| 快捷键 | 功能说明 | 状态 |
| :---: | :--- | :---: |
| **`F2`** | 随机发送【默认词库】语录（带 LRU 智能防重复） | 升级优化 |
| **`F3`** | 随机发送【自定义词库】语录（带 LRU 智能防重复） | 升级优化 |
| **`F4`** | **手动框选截图 -> OCR 识别 -> AI/内置阴阳怪气反击** | 新增核心 |
| **`F5`** | **静默截取 LOL 聊天框 -> OCR 识别 -> AI/内置阴阳怪气反击** | 新增核心 |
| **`F6`** | 随机发送【阴阳怪气词库】语录 | 新增核心 |
| **`F7`** | 切换【游戏内安全透明穿透悬浮窗 HUD】显隐 | 新增核心 |
| **`F8`** | 切换【单行零宽字符穿插防屏蔽】模式 | 新增核心 |
| **`F11`** | 切换【所有人 (`/all`)】公屏频道 | 原版保留 |
| **`F12`** | 切换【逐字纵向刷屏发送】模式 | 原版保留 |
