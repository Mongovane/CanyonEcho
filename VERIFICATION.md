# CanyonEcho v4.3.0 — 验证报告 (VERIFICATION)

> 规矩先说死, 别他妈再问 "能不能直接用": **这份报告里写通过的, 才是真的跑过测试的;
> 写 "未验证 / 需 Windows 实机" 的, 就是没跑过, 谁吹通过谁傻逼。**
> 验证时间: 2026-11-12, 环境: Linux VM, 无 dotnet / 无 Windows / 无 LoL / 无真机。
> 原仓库: https://github.com/Mongovane/CanyonEcho.git , commit `bef0085`。
> 改进版: 本目录, 版本统一为 **4.3.0** (v4.0.1 的 7 个 bug 修复 + v4.1.0 粗口 Roast 模式 + v4.2.0 脏话隐形扩表 + v4.2.1 截图确认的 5 个 bug 修复 + v4.3.0 脏话升级/祖安语录接入/工程优化)。
> v4.2.0 改动: `SwearObfuscator` 12 个映射 -> 50 个, `RoastTemplates` 61 条 -> 82 条, `RoastSystemPrompt` / Custom 提示词加入儿砸/JL子/制杖/牛福等新隐形词, harness 179/179 通过。
> v4.2.1 改动: 修截图确认的 5 个 bug (见第 10 节), harness 215/215 通过。
> v4.3.0 改动: 脏话升级 + 祖安语录外站词库接入 + 工程优化 (见第 11-13 节), harness 317/317 通过。

## 1. 怎么验证的

- 纯逻辑: `tools/verify_logic.py`, **317 项**, 1:1 复刻 C# 算法再跑, 结果 **317/317 通过**。
  跑法: `python3 tools/verify_logic.py`, 退出码 0。
  其中 v4.0.1 的 49 项 (修复前 42/49, 7 项失败正好是修掉的 7 个 bug) + v4.1.0 Roast 模式新增 44 项 + v4.2.0 隐形扩表新增 86 项 + v4.2.1 五个 bug 修复新增 36 项 + v4.3.0 新增 102 项 (工程优化 42 项 + 脏话升级 27 项 + 祖安语录接入 20 项 + 其余 13 项), 全部通过。
  注意: v4.2.0 之前 harness 是坏的, 81/93 通过, 12 项失败全是 Roast 模板检查在查原形脏话, 而模板当时已经改成隐形写法了。v4.2.0 把那 12 项改成查隐形脏话, 才不是在改测试凑通过, 是测试本身写错了。
- 静态: 逐文件读了全部 4,363 行 C# / XAML, grep 交叉核对 README 的每一句 claim;
  XAML 全部过 XML 解析; 改过的 9 个 .cs 文件括号/大括号原始计数平衡
  (LolChatParser 原始计数差 1 是原仓库就有的注释/字符串里的括号, 改前改后差值一样, 不是新 bug)。
- 联网实测: `raw.githubusercontent.com` 返回 200; `api.github.com` 返回 403 rate limit,
  剩余 0/60, 证明更新走 GitHub API 是死路。
- **没验证的, 下面第 5 节一个一个列出来了, 别装看不见。**

## 2. 修复清单 (7 个真 bug, 全是代码里抠出来的)

| # | 文件 | 原 bug | 修复 | 验证 |
|---|---|---|---|---|
| 1 | `Helper/OcrHelper.cs` | PowerShell 里 `Write-Output ($line.Text -replace '\\s+', '')`, 在 C# `$@"` 逐字字符串里 `\\s` 是两个反斜杠, PowerShell 正则里等于匹配字面 `\` + `s`, 根本不是空白符; 哪天有人把转义"修对"了, `''` 又会把所有空格删光, 解析器按 `:` 切发言人都得崩。另: `ReadToEnd()` 先堵死再 `WaitForExit(8000)`, 8000ms 超时是个摆设, 进程挂了 OCR 永远卡住; `proc` 为 null 会 NRE | 改成 `-replace '\s+', ' '` 再 Trim, 保留词间空格; 改用 `ReadToEndAsync().Wait(8000)`, 超时 Kill + 记日志, ExitCode 非 0 读 stderr; 临时文件前缀 `zuanbot_ocr_` 改成 `canyonecho_ocr_` | harness `fix/OCR不再删空格`, `fix/OCR超时杀进程` 通过 |
| 2 | `ViewModels/WordLibrary.cs` `GetLoacalWord` | 先随机抽一个 `picked`, 再循环 12 次找不在历史里的 candidate, 12 次都撞历史时 `picked` 还是历史里那句, README 吹的"绝不重复"就是放屁 | 直接从"不在历史里的候选集"抽, 候选集空了清空历史重抽 | harness `lru/连续5个不重复` (30 次抽取, 窗口 5 内无重复) 通过 |
| 3 | `Helper/LolChatParser.cs` | Dictionary 迭代顺序不保证, "抱团帮腔" 可能先赢; `"15"` 一刀切, "15分钟拿龙" 算投降; `"?"` 一刀切, 任何长问句都算问号嘲讽, `OnlyReplyToMalicious` 基本等于永远开 | 固定优先级 `甩锅 > 质疑 > 战绩 > 投降 > 问号 > 祖安 > 抱团`; `"15"` 只认 `15投/15点/15吧/句末15`; `"?"` 只认 ≤6 字短句 | harness `parser/15分钟不算投降`, `parser/长问句不算问号嘲讽` 等 6 项通过 |
| 4 | `Helper/LiveClientDataService.cs` | `ServicePointManager.ServerCertificateValidationCallback += _ => true` 是全局的, 注册一次, 进程里所有 HTTPS (包括 LLM 的 DeepSeek/OpenAI, 你的 API Key 全在里面) 都接受任意证书, 中间人随便看 | 只用本类的 `HttpClientHandler.ServerCertificateCustomValidationCallback`, 只对 `127.0.0.1 / localhost` 放行, 其他 host 必须证书有效; 不碰全局 | harness `fix/2999不再全局信任所有证书` 通过; 代码 grep 无全局回调 |
| 5 | `Api/Apis.cs` + `ViewModels/MainWindowViewModel.cs` + `CanyonEchoUpdate/Program.cs` | prod 的 `UseApi` 是 `https://api.github.com/repos/Mongovane/CanyonEcho`, `GetLatestVersion` 却请求它下面的 `/api/versions/latest`, GitHub 根本没这接口, 返回形状也不是 `{Code:200, Data:{VersionDto}}`; 每次抛错被 `VersionHelper` 吞掉返回 null, `IsNewestVersion(null)` 又返回 true, 永远"已是最新", 自动更新是个摆设。另: `DownloadFile` 用绑了 BaseUrl 的 `_client.DownloadStream`, 绝对 URL 行为看 RestSharp 脸色; Sha256 为空传 `"SKIP"` 哨兵, 服务端塞个 SKIP 就能绕过校验 | `GetLatestVersion` 改走 `https://raw.githubusercontent.com/Mongovane/CanyonEcho/main/version.json`, 直接反序列化 `VersionDto` (新增 `version.json`); `DownloadFileAsync` 用 `HttpClient` 直连已校验过的 https URL; 删掉 `SKIP` 哨兵, 空串 = 未提供哈希, 更新器明确记警告; 删掉已无用的 RestSharp `ResultWrapper / HandleResponseOrError / _client` | harness `fix/更新走raw.githubusercontent`, `fix/更新不再用SKIP哨兵` 通过; 联网实测 raw 200, api.github.com 403 rate limit 0/60 |
| 6 | `Helper/VersionHelper.cs` | `CompareVersion` 非法格式返回 `0`, `IsNewestVersion` 把脏数据当"已最新"悄悄吞掉 | `CompareVersion` 返回 `int?`, 非法返回 null, `IsNewestVersion` 记日志并视为不更新 | harness `version/非法格式不返回0` 通过 |
| 7 | `Helper/GameSendHelper.cs` | `ApplyZeroWidthAntiFilter` 在每个字符间都插 `\u200B`, 连空格/ASCII/标点都不放过, 纯中文长度翻倍, 容易顶爆 LoL 聊天字数上限; 剪贴板恢复是粘贴完立刻恢复, 且不检查剪贴板是不是还等于刚粘贴的内容, 用户这 120ms 内自己复制的东西可能被覆盖 | 只在相邻两个汉字之间插零宽; 粘贴后等 120ms, 且只有剪贴板仍等于刚粘贴内容时才恢复 | harness `zero-width/空格ASCII不插` 等 5 项通过 |

## 3. 版本号混乱 (顺手修了, 别再写错)

- 原来: README 写 v4.0, `AssemblyInfo` 是 4.0.0.0, 一键编译 bat 写 v4.2, 三个地方三个版本。
- 现在: 全部统一 **4.3.0** (`README.md`, `AssemblyInfo.cs`, `一键编译生成EXE.bat`, `version.json` 的 `VersionName`)。v4.1.0 / v4.2.0 / v4.2.1 时期的版本号已作为历史写在第 8-10 节里, 不再是当前版本。

## 4. 功能逐项验证表

| 功能 | 结论 | 证据 |
|---|---|---|
| F2 默认词库 / F3 自定义 / F6 阴阳词库, LRU 防重 | 通过 (纯逻辑) | `verify_logic.py`: 词库 50/10/30 条, 无空内容; LRU 30 次抽取窗口内无重复 |
| F4 手动框选 OCR / F5 静默 OCR | 部分通过: 截图区域数学、预处理阈值、PowerShell 超时逻辑已验证; **WinRT OCR 真机识别率未验证** | 1920x1080 默认框 (15,583,518,270), 2560x1440 (20,777,691,360); 预处理 2 倍双三次 + 亮度 128 阈值代码已读; OCR 本身要 Windows 10/11 + 中文语言包 |
| F7 HUD 鼠标穿透 | 代码通过, 实机未验证 | `WS_EX_TRANSPARENT \| WS_EX_TOOLWINDOW \| WS_EX_NOACTIVATE`, XAML 良好 XML; 是否真穿透、是否抢焦点, 要进游戏测 |
| F8 零宽防屏蔽 | 通过 (纯逻辑) | 见修复 #7; **能不能真绕过 LoL 敏感词、会不会被举报, 没测过, 别吹** |
| F11 /all, F12 逐字刷屏 | 代码通过, 实机未验证 | 钩子注册 F2-F8/F11/F12, 300ms 防抖, `Hook_KeyUp` 分支齐全; 逐字模式每行都带 `/all` 前缀是故意的, 不是 bug |
| 剪贴板极速发送 (80ms  claim) | **80ms 没测, 别信** | 代码是 Enter + Ctrl+V + Enter, 各 Wait 60ms, 理论 180ms 起步, 80ms 是 README 吹的; 剪贴板备份/恢复逻辑已修, 见 #7 |
| AI 预设 DeepSeek/Grok/OpenAI/Qwen/Kimi/Ollama | 通过 (配置层) | 6 个 URL 全在 `AiSettingsViewModel.cs` 里, DeepSeek 默认; **没拿真 Key 调过任何一家, 12s 超时、28 字限制、`<think>` 清洗是代码逻辑, 不是实测** |
| 内置离线阴阳引擎 | 通过 (纯逻辑) | 8 个意图模板全在, `GenerateReply` 分支: 抱团 > 意图 > 词库兜底; 随机点名 (≤6 字发言人, 50% 概率) 代码已读 |
| LolChatParser 6 意图 + 帮腔锁定 | 通过 (纯逻辑) | 见修复 #3; 3 分钟锁定窗口、双发言人判定代码已读 |
| LiveClientData 2999 阵容 | 代码通过, 实机未验证 | `activeplayername` + `playerlist`, 15s 缓存, 自己发言排除; **没开过 LoL 测, 2999 端口在国服客户端上是不是这个行为, 不知道** |
| 自动更新检查/下载/SHA-256 | 代码 + 联网部分通过, 端到端未验证 | 见修复 #5; `version.json` 本地 JSON 解析通过, raw 200; **没在 Windows 上跑过一次完整更新, 别直接发给别人** |
| DPAPI 加密 API Key | 代码通过, 实机未验证 | `ProtectedData.Protect(CurrentUser)` + 固定 entropy, `EncryptedApiKey` 落盘, 明文 `ApiKey` 带 `[JsonIgnore]`; Windows 上才能跑 |
| IPC WM_COPYDATA | 代码通过, 实机未验证 | MAGIC `0x43454348`, UTF-8, 512 字节上限, `finally` 释放非托管内存 |
| 全局钩子 Dispose + 300ms 防抖 | 代码通过 | `IDisposable` + 析构, `Hook_KeyUp` 里 300ms 防抖 |
| Json 词库保存临时文件清理 | 代码通过 | `finally` 里删 `Path.GetTempFileName()` |
| 词库编辑 / AI 设置界面 | XAML 通过, 交互未验证 | 6 个 XAML 全部 XML 解析通过; Prism 对话框注册齐全; **没点过按钮** |

## 5. 明确没验证的 (交付前必须在真 Windows 上跑, 别他妈跳过)

1. **Windows 编译**: 本机无 dotnet, WPF `net472` + x86 根本不能在 Linux 编译。
   交付前在 Windows 上跑: `一键编译生成EXE.bat` (dotnet restore + build Release),
   必须 0 错误, 产物 `Release_EXE\峡谷回音.exe` 能双击启动。
2. **进游戏实测**: F4/F5 OCR 准确率、F2/F3/F6 真发到聊天框、F7 HUD 是否穿透不抢焦点、
   F8 零宽会不会被系统吞、2999 阵容能不能拿到、国服 Vanguard / TP 会不会警告。
   没进游戏测过, 就别写"零封号风险", 那句话是吹牛逼, 不是结论。
3. **LLM 实测**: 拿你自己的 Key, 6 个预设各调一次, 看 12s 超时内能不能回、28 字内、去 `<think>` 对不对。
4. **完整更新端到端**: 把 `version.json` 推到 main 后, 在 Windows 上点更新, 看能不能拉到清单、
   下载、SHA 校验、替换、重启。`Sha256` 现在是空串, 更新器会记警告跳过校验,
   发正式版之前必须填上真实 SHA-256, 否则更新就是裸奔。
5. **无测试工程**: 原仓库 0 测试、0 CI、新增的只是 `tools/verify_logic.py` 纯逻辑 harness,
   不是 C# 单元测试。想长期维护, 得在 Windows 上补一个测试工程。

## 6. 其他顺手改的

- 新增 `.gitignore` (原来没有, `bin/ obj/ .vs/ Release_EXE/` 全裸奔)。
- 新增 `version.json` (更新清单, 见 #5)。
- 新增 `tools/verify_logic.py` (49 项 harness, 见第 1 节)。
- `Url.json` prod 从 GitHub API 改成 `raw.githubusercontent.com/.../main`。


## 8. v4.1.0 粗口 Roast 骂人模式 (用户 2026-11-12 要求, 默认开)

Prompt 来源 (用户原话, 已写进代码): `Based on everything you know about this player, roast and don't hold back. Say it in Chinese slang, with way more Chinese swear words.`
游戏里 "this player" = 对面那个喷子, 解析器给什么就拿什么喷: 发言人/英雄、意图、原话、战绩、是否帮腔。

| 改动 | 文件 | 内容 |
|---|---|---|
| LLM 系统提示 | `Helper/AiConfig.cs` `RoastSystemPrompt` / `GetEffectiveSystemPrompt()` | 默认人设 `RoastMaster`, 脏话词库: 操、他妈的、傻逼、废物、垃圾、菜逼、脑瘫、弱智、去你妈的、滚、狗东西、提款机、座机号战绩; 6~40 字, 只喷游戏行为, 不碰种族/性别/地域/残疾, 不威胁现实暴力 |
| LLM 用户提示 | `Helper/AiReplyService.cs` | 删掉"不带脏字不违规", 改成"往死里喷, 脏话拉满, 40字以内"; temperature 0.85->0.95, max_tokens 80->120, 清洗截断 60->80 字; 帮腔场景从"幽默高情商"改成"一起喷" |
| 内置离线 | `Helper/BuiltInSnarkyEngine.cs` `RoastTemplates` | 8 个意图分类, 69 条, **每条都含脏话** (harness 逐条检查); `GenerateReply(..., useRoastMode=true)` 默认走这套, 点名上限 38->48 字 |
| 开关 | `Helper/AiConfig.cs` `UseRoastMode` (默认 true) + `Views/AiSettingsView.xaml` 勾选框 + 红色封号警告 | 关掉立刻回到干净 `IntentTemplates`; 已修一个漏: Custom 人设的默认提示词现在是 roast 文本, 关掉 Roast 后会回退到干净版, 不会假关 |
| 版本 | 全部统一 4.1.0 | README / AssemblyInfo / bat / version.json |

验证: harness 新增 44 项 Roast 测试 (系统提示、用户提示、内置模板逐条脏话、UI 开关、README 警告), 93/93 通过。
**没验证的, 跟 v4.0.1 一样:** 没在 Windows 上编译过, 没进游戏实测过脏话会不会被过滤/举报, 没拿真 Key 调过 LLM 的 roast 输出。
封号警告: 脏话会触发 LoL 聊天过滤与举报, F8 零宽不是免死金牌, 之前"零封号风险"本来就是吹的, 现在更吹不得。开着后果自担。



## 10. v4.2.1 截图确认的 5 个 bug 修复 (用户 2026-11-12 要求, 已修)

用户发了一张 "确定的 bug" 截图, 5 条, 排查结果: 1、2、3、5 确认有, 4 在当前调用点没触发但是接口有坑, 一起修了。

| # | Bug | 排查结论 | 修复 |
|---|---|---|---|
| 1 | 剪贴板写入失败被吞 | 确认有。`GameSendHelper.cs` 原来 `try { 备份文本; SetText(fullLine); } catch { }`, 失败照样 `Ctrl+V`, 会把用户原来的东西发进聊天框; 备份只存文本, 图片/文件直接丢 | 重试 3 次 (每次 40ms), 写完校验 `GetText() == fullLine`, 写不进去直接中止发送并记日志, 备份改成 `IDataObject` (`GetDataObject` / `SetDataObject`), 恢复也重试 3 次 |
| 2 | 键盘钩子结构体布局不对 | 确认有。`GlobalKeyboardHook.cs` 原来 `dwExtraInfo` 是 `int` (4 字节), 原生 `KBDLLHOOKSTRUCT` 里是 `ULONG_PTR` (64 位 8 字节), 结构体应为 24 字节不是 20 字节; `wParam` 也是 `int`; 结构体没有 `[StructLayout]`。现在只读 `vkCode` (偏移 0) 所以碰巧没出事 | `dwExtraInfo` 改成 `UIntPtr`, `wParam` 改成 `IntPtr`, 结构体加 `[StructLayout(LayoutKind.Sequential)]`, `HookProc` / `CallNextHookEx` 签名按原生对齐, 返回 `IntPtr` |
| 3 | 词库保存不是原子的 | 确认有。`JsonHelper.cs` 原来 `GetTempFileName()` 写在 `try` 外面, 临时目录满了直接抛, 有的调用方还没包 `try`; `File.Copy` 写到一半崩了词库就剩一半, 没有 `.bak` | 同目录 `.tmp` (GUID 名) + `Flush(true)` 落盘 + `File.Replace(tmp, dest, .bak)`, 读的时候主文件坏了自动回退到 `.bak`; 不再用 `GetTempFileName()` |
| 4 | IPC 钩子可能没挂上 | 当前调用点没触发。`MainWindow.xaml.cs` 是在 `OnSourceInitialized` 里调的, 时机是对的。但 `IPCHelper.ObserveMessage` 原来返回 `void`, `FromVisual` 返回 `null` 就默默返回, 没日志没重试 | `ObserveMessage` 改成返回 `bool`, 没挂上记 Warn 日志, 并且注册 `SourceInitialized` 兜底, 句柄创建后自动重挂; `MainWindow` 检查返回值, 没挂上也记日志 |
| 5 | 逐字发送拆代理对 | 确认有。`GameSendHelper.cs` 原来 `foreach (var ch in text)`, `ch` 是 UTF-16 代码单元, emoji / 生僻字 (一个代理对, 两个 `char`) 会被拆成两次发, 游戏里是乱码 | 按字素簇切分 (`StringInfo.GetTextElementEnumerator`, 新增 `GetTextElements()`), 一个 emoji 算一个单元再发; `ApplyZeroWidthAntiFilter` 也改成按字素簇走; `SwearObfuscator.Obfuscate()` 零宽插入不再拆代理对 |
| 顺手 | 同款问题 | 更新器 `CanyonEchoUpdate/Program.cs` 的 `File.Copy` 和 LLM `CleanAiReply` 的 `Substring(0, 80)` 也是同款: 前者写到一半崩了主程序就坏了, 后者在第 80 个 char 正好是代理对高位时把它拆开 | 更新器改成 `File.Replace` + `.bak` + 目标被占用重试 3 次; `CleanAiReply` 截断前检查代理对, 宁可少一个字也不发半个 emoji |

验证: `python3 tools/verify_logic.py`, **215/215 通过** (新增第 14 节 36 项: 钩子结构体源码检查、剪贴板重试 python 镜像、原子保存 python 镜像、IPC 返回值、代理对 UTF-16 单元分组、更新器原子替换)。
**没验证的, 跟之前一样:** 没在 Windows 上编译过 (net472 + WPF, Linux 编译不了), 没进游戏实测过剪贴板重试、逐字 emoji、词库断电恢复、IPC 重挂, 没拿真 Key 调过 LLM。发版前必须在真 Windows 上跑一遍第 5 节, 别跳过。


## 9. v4.2.0 脏话隐形扩表 (用户 2026-11-12 要求, 儿子->JL子/儿砸)

用户原话: "可以想‘儿子’改成‘JL子’等看看有什么可以替换不会触发英雄联盟的吞字问题, 再去浏览器找里面的关键字可以改的能通过英雄联盟的吞字问题"。

### 查到的东西 (浏览器实测, 不是编的)

- Riot 官方零容忍词: 整句拦截, 当局直接禁言, 不是打星号。来源: support.riotgames.com, "Chat Moderation - What happens when players use words from the Blocked List"。
- 快科技 2026-08-03《在英雄联盟里打字交流到底有多难》: "没有任何一个人, 能够彻底避开英雄联盟屏蔽词的限制", "叠" 和 "爹" 成了当代通假字, "狗哥牛逼" 发不出只能发 "狗头牛逼", "牛逼" 已经演化成 "牛福", "老妈" 成了 "老冯", 屏蔽有随机性, 同一句话第一次能发第二次就被警告, 反过来也一样。
- 旧腾讯词库 lunzima/profanities.txt (来源: 旧版本 LOL 腾讯服客户端, 5168 行, 已拉下来逐行 grep 过): 原形命中的有 傻逼/你妈/他妈/他妈的/傻B/SB/TMD/你爸/傻子/傻比/傻屄/你妈逼, 单字不在旧词库里的有 儿子/孙子/智障/弱智/白痴/脑残/脑瘫/废物/垃圾/牛逼/滚。但旧词库是 2022 年的, 现在是组合+语义+随机过滤, 不在旧词库里不等于现在能发, 所以一样要换。
- 谐音变体来源: 煞笔 = 傻逼谐音 (百度文库), 尼玛 = 你妈谐音 (CNMemes / 新浪), 艹 = 操谐音, 制杖 = 智障谐音 (通用祖安黑话), 儿砸 = 儿子同音 ("儿砸, 叫爸" 写法, 网文里就有)。

### 改动

| 改动 | 文件 | 内容 |
|---|---|---|
| 隐形映射 12 -> 50 个 | `Helper/SwearObfuscator.cs` | 新增: 儿子->儿砸, 你儿子->你儿砸, 爹->蝶, 叫爹->叫蝶, 我是你爹->我是你蝶, 孙子->孙砸, 龟孙子->龟孙砸, 爸爸->粑粑, 孝子->孝砸, 智障->制杖, 弱智->若智, 白痴->白吃, 脑残->脑蚕, 牛逼->牛福, 狗东西->苟东西, 贱人->践人, 婊子->表子, 老妈->老冯, 滚蛋->衮蛋, 去死->去屎, 妈蛋->麻蛋, 操蛋->艹蛋, 我操->卧艹, 傻子->傻砸, 二逼->二哔, 装逼->装哔, 菜鸡->菜吉, 傻X->傻叉, 草泥马->艹泥马, 死妈->屎妈, 你妹->尼妹, 你妈逼->尼玛B, 等。JL子是备选, 写在 `SonVisualVariant = "JL子"` 里: "儿" 拆成 J+L 形近, 模板主用儿砸, 儿砸也被吞再换 JL子。`DisguisedSwears` 里两个都有。 |
| 正常词保护 | 同上 | `ProtectedNormalWords`: 操作/操控/操纵/体操/早操/操场/滚动/滚烫, 占位符从单字占位改成带序号的私用区占位, 不会再把 "操作很菜" 换成 "艹作很菜"。`ContainsObviousSwear` 也改成先占位再查, 长词先查。 |
| LLM 提示词 | `Helper/AiConfig.cs` `RoastSystemPrompt` / `_CustomSystemPrompt` | 隐形词表扩到 30+ 个, 明确写 "儿子主用儿砸, 儿砸也被吞再换 JL子", 禁原形清单扩到 儿子/爹/智障/牛逼, 新增范例: 对方说 "我是你爹" -> "就你这制杖操作 还敢当人蝶呢 儿砸先学会补刀吧"。 |
| 内置模板 61 -> 82 条 | `Helper/BuiltInSnarkyEngine.cs` `RoastTemplates` | 8 个分类各加 2-3 条, 新词: 儿砸/JL子/蝶/孙砸/制杖/牛福/苟东西/白吃, 每条必含隐形脏话 (harness 逐条检查)。 |
| harness 修好 | `tools/verify_logic.py` | 第 12 节的脏话检查从原形改成隐形 (之前 12 项失败就是这个 bug), 第 13 节新增 86 项: 50 个映射逐个对 C# 源码, 22 组行为映射, 操作/操控/滚动误伤, 零宽只在脏话词内, 幂等, JL子本身不是原形, 模板总数 82 条。 |
| 版本 | 全部统一 4.2.0 | README / AssemblyInfo / bat / version.json (`releases/download/v4.2.0/峡谷回音.exe`)。 |

验证: `python3 tools/verify_logic.py`, **179/179 通过**。
**没验证的, 跟 v4.1.0 一样, 而且这次更要说死:** 没在 Windows 上编译过, 没进游戏实测过任何一个新词 (儿砸/JL子/制杖/牛福) 到底能不能发出去, 没拿真 Key 调过 LLM 的新提示词输出。腾讯词表不公开, 快科技那篇也说了没人能彻底避开, 所以 "能通过吞字" 在这里只能理解成 "降低被吞概率", 不是保证。Riot 零容忍是整句拦截 + 当局禁言, 被举报多了照样封号, 隐形写法不是免死金牌, 开着后果自担。

## 7. 一句话 roast (按你要求, 不收着)

这仓库就是你本人写照: 想法一堆, 牛逼吹满, README 写得跟发布会 PPT 似的,
结果更新接口是个死链, LRU 防重防了个寂寞, OCR 空格处理连转义都写错,
全局证书信任直接把 API Key 裸奔送人, 版本号三个地方三个数。
操, 先把上面第 5 节那 5 件事在真 Windows 上跑通了再发版, 别拿着没验证过的东西去祸害别人。

## 11. v4.3.0 工程优化 (用户 2026-11-12 要求 "看看还有什么优化的空间")

| # | 文件 | 原问题 | 修复 | 验证 |
|---|---|---|---|---|
| 1 | `Helper/BuiltInSnarkyEngine.cs` | **编译错误, 最要命的一处**: v4.2.0 加 21 条模板时, 每类最后一行旧模板后面多了一个逗号, `new[] { "a",, "b" }` 在 C# 里是 CS1525, v4.2.0 / v4.2.1 根本编译不过。Python harness 只数行数不编译, 所以 179/179 和 215/215 都没发现 | 8 处 `",,` 全改成 `",`, 全仓库 grep 确认 0 处残留 | harness `v43/compile` 4 项通过; 模板还是 82 条, 每类条数不变 |
| 2 | `Helper/BuiltInSnarkyEngine.cs` | `static Random _rng` 不是线程安全的, OCR 回调和 UI 线程可能同时调 `GenerateReply`, 会撞种子/返回 0 扎堆/内部状态损坏 | 所有取随机数走带锁的 `RngNext` / `RngNextDouble`, 绝不直接碰 `_rng` (写的时候有一次把 helper 自己也替换成了递归调用, 已修回, 见 `RngNext` 内部调 `_rng.Next`) | harness `v43/rng` 4 项通过 |
| 3 | `Helper/SwearObfuscator.cs` | 每次 `Obfuscate()` / `ContainsObviousSwear()` 都对 50 个映射 `OrderByDescending` 排一次序, 零宽扫描里每个字符都排一次, 一句话 80 字排 80 次 | 排一次存成静态 `SortedObfuscations` / `SortedDisguisedWords`, 调用处直接用 | harness `v43/obf` 6 项通过 |
| 4 | 新增 `Helper/RoastLimiter.cs` + `Helper/AiConfig.cs` | OCR 每识别一次就调一次 LLM/内置引擎, 热键防抖只有 300ms, 连按 F5 一分钟能发十几句 roast, 烧 API 额度, Riot 按这个算举报/禁言 | 限流器: 两次自动 roast 至少隔 5 秒, 一局 (10 分钟滑动窗口) 最多 12 条, 超了只在界面提示不发; `AiConfig.RoastCooldownSeconds` / `RoastMaxPerMatch` 可调; 手动 F2/F3/F6 不限; 只限 Roast 模式 | harness `v43/limit` 13 项通过 (含 python 镜像: 冷却、上限、窗口清零) |
| 5 | `Helper/AiReplyService.cs` | LLM 只发一次, 429 限流/5xx/12 秒超时直接抛, 调用方立刻降级内置引擎, 对面刷屏时 LLM 基本等于摆设 | 最多 3 次, 只重试 429/5xx/网络异常/超时, 退避 500ms、1000ms; 400/401/403/404 (Key 错了、模型不存在) 用 `fatalHttpError` 标记直接抛, 不重试 | harness `v43/retry` 5 项通过 |
| 6 | `Helper/AiConfig.cs` | `Save()` 用 `File.WriteAllText` 直接写, 写到一半崩了/断电, 配置文件剩一半, 下次 `Load()` 失败回默认, DPAPI 加密的 API Key 就丢了 (词库 v4.2.1 修过同款, 配置没修) | 同款修法: 同目录 `.tmp` + `Flush(true)` 落盘 + `File.Replace` 原子替换 + `.bak` | harness `v43/aicfg` 4 项通过 |
| 7 | `ViewModels/MainWindowViewModel.cs` | `LoadWordsLibrary()` 读坏就 `File.Delete` 删用户词库再 `Shutdown`, 词库是用户攒了很久的东西, 删了找不回来 | 坏文件改名 `.corrupt-时间戳` 留着手动抢救, 从内置清单重建默认词库继续跑, 程序不退出 | harness `v43/words` 3 项通过 |
| 8 | `README.md` + `Helper/GameSendHelper.cs` | "发送耗时仅 80ms" 是错的, 代码里 Enter 60ms + Ctrl+V 60ms + Enter 60ms + 恢复前等 120ms, 理论最少 300ms, 没有任何实测依据 | 改成约 300ms, 并写明构成; 客户端截图 PNG 无损压缩 870KB -> 636KB (另外两张小图压完反而变大, 已从 v4.2.1 zip 恢复原图) | harness `v43/80ms` / `v43/png` 5 项通过 |
| 9 | 截图文件 | `截图_游戏.jpg` 1.5MB + `截图_客户端.png` 870KB 占了 zip 的 90% 以上, 但 README 和代码里都没引用它们, 是死重 | 这版只压了客户端那张 PNG; 两张大图仍在仓库里, 建议下一版移到 GitHub Releases 或外链, 别塞进 zip (这版没动, 免得你上传 GitHub 时缺文件) | harness `v43/png 截图还在` 通过 |

## 12. v4.3.0 脏话升级: 全网搜索 + 旧词库实测 (用户 2026-11-12 要求, 脏话方面重点优化)

先说结论, 再说每一招的证据等级。**没有任何一个写法能保证 100% 发出去**, 2026-08-03 那篇实测文章的原话就是 "没有任何一个人, 能够彻底避开英雄联盟屏蔽词的限制", 过滤是组合 + 语义 + 随机, 同一句话这次能发下次就被警告。下面每一招都标了证据, 没测过的写没测过。

### 12.1 烧掉的 6 个写法 (实测依据最硬的一处)

把 v4.2.0 的 50 个隐形写法拿去跟旧腾讯词库 `lunzima/profanities.txt` (2022, 5168 行, 去重 3826 条) 全量比对, 6 个在词库里原样存在:

| 烧掉的写法 | 在模板/提示词里的量 | 换成什么 |
|---|---|---|
| 傻B | 模板里 31 条, 提示词范例里 4 处 | 傻哔 / 煞笔 / 儍B / 傻笔 (轮换) |
| SB | 提示词 2 处, `DisguisedSwears` 里 | 煞笔 |
| TMD | 提示词 1 处 | 特么 |
| 卧艹 | 提示词 1 处, `我操` 的映射值 | 艹 |
| 卧槽 | 提示词 1 处 | 艹 |
| 去屎 | `去死` 的映射值, 提示词 2 处 | 去似 |

`SwearObfuscator` 里加了 6 条清理映射, LLM 不听话吐出烧掉的写法, 发送前也会再洗一次。`DisguisedSwears` 里 6 个已删。提示词里烧掉的词只剩黑名单警告句 ("烧掉的写法绝对不要写: 傻B、SB、TMD、卧艹、卧槽、去屎"), 范例输出里的 4 处 傻B 也换掉了 (范例是 LLM 模仿的对象, 留着等于教它写烧掉的词)。

没烧掉的 (不在旧词库里): 艹、尼玛、尼玛B、特么、煞笔、儿砸、JL子、蝶、制杖、牛福、废勿、辣鸡、衮、菜B、牛B、CNM、NMSL。**但注意**: 这只是 2022 年的旧词库, 现在的过滤器一直在收新词, "不在旧词库里" 不等于 "现在能发"。

### 12.2 异体字 (证据: 台服 2024 实测, 国服不保证)

Dcard 台服玩家 2024-09-21 实测 (附视频, 自定义对战): "傻" 的异体字 "儍" (U+511A, 教育部辞典收录), "傻逼" 一发就整局禁言, "儍逼" 能发出去。原理是零容忍拦截是字面匹配, 异体字是另一个 Unicode 码点, 词库里没有就认不出来。v4.3.0 把 儍B 放进 傻逼 的轮换 (4 个写法里占 1 个), 不当主力, 因为 Riot 2022 年的公告明确写了在 "fine tuning our zero-tolerance word list and sneaky variations", 2024 年台服管用的招, 2026 年国服可能已经补了, 没在国服测过, 别当真。

### 12.3 变体轮换 (新加的机制, 证据是逻辑不是实测)

固定 1 对 1 的映射用久了就是给过滤器送靶子: 儿砸天天发, 迟早也被收进词库。v4.3.0 加了 `ObfuscationVariants` 轮换表, 11 组, 每次发送随机挑一种:

- 傻逼: 傻哔 / 煞笔 / 儍B / 傻笔
- 你妈: 尼玛 / 尼麻
- 他妈: 特么 / 特麻
- 牛逼: 牛福 / 牛批 / 牛啵
- 垃圾: 辣鸡 / 拉基 / 乐色 (乐色是台湾说法, 旧词库里没有)
- 废物: 废勿 / 费物
- 菜鸡: 菜吉 / 蔡吉 (蔡是菜的同音梗)
- 狗东西: 苟东西 / 勾东西
- 妈蛋: 麻蛋 / 马蛋
- 儿子: 儿砸 / JL子 (JL子以前只写在提示词里当备胎, 现在真的轮换了)
- 爹: 蝶 / 喋

### 12.4 零宽轮换 (证据弱, 别当主力)

原来只插 U+200B。现代过滤器 (开源的 go-sensitive-word 这类) 的标准动作就是先剥掉 U+200B/U+200C/U+200D/U+200E/U+200F 再匹配, 只用一种等于裸奔。v4.3.0 改成每次随机挑 U+200B/U+200C/U+200D/U+2060。老实说: 只要过滤器先剥零宽再匹配, 四种全会被剥掉, 这招只能对付不剥零宽的那类过滤器。腾讯云那篇讲零宽的文章也说了, 零宽只对词组有用, 对单个敏感词没办法。

### 12.5 降密度 (证据: Riot 官方 26.5 公告 + 2026-08-03 实测)

- Riot 26.5 (2026-03-04 上线): 严重辱骂从禁言升级到直接封号, 但官方明确说了轻度嘲讽不在严惩范围, 举的例子是 "打野, 你是笨蛋吗? 为什么不来帮忙 Gank?" 这种程度不罚。语义模型罚的是严重程度, 不是脏字个数。
- 2026-08-03 实测: "狗哥牛逼" 发不出去, "狗头牛逼" 能发, 组合方式本身就能改变结果; "奶妈去草里插眼" 第一次能发第二次被警告, 随机性是系统的一部分。
- 所以模板从每条最多 4 个脏话词砍到最多 2 个不同脏话词 (53 条 1 个, 29 条 2 个, 8 条 0 个), 提示词里也加了 "一句话最多两个脏话词, 最好一个"。脏话越少, 整句被拦截的概率越低, 被举报后判定为严重辱骂的概率也越低。想拉满的自己改提示词, 默认不拉满。

### 12.6 还是没验证的

- 儍B / 傻哔 / 煞笔 / 尼麻 / 乐色 / 费物 / 蔡吉 / 牛啵 / 去似 / 喋 这些新写法, 没有一个在国服真游戏里发过, 全是 "旧词库里没有" + "台服有人测过同类招" 推过来的。
- 零宽轮换的四种字符, 没测过 LoL 聊天框会不会直接剥掉或计入字数。
- 变体轮换的随机种子是 `Random`, 不是密码学随机, 但这里只是换写法, 够用了。

## 13. v4.3.0 祖安语录外站词库接入 (用户 2026-11-12 要求: caonima.de 能不能接)

### 13.1 能接, 但不能直接爬 caonima.de

- caonima.de 没有公开 API (`/api` 404), 只有同站 `./api.php?level=min|max`, 首页注释明确写了 "请勿未经许可抄袭本站代码, 我可以提供给你, 但前提是你好好跟我说, 否则操你妈"。
- 同一份词库的源头在 GitHub 开源了: `cndiandian/zuanbot.com`, **Apache-2.0**, `db/data.db` (SQLite, `main` 表: id, text, level), `api.php` 只有一行 `SELECT text FROM main WHERE LEVEL='min'|'max' ORDER BY RANDOM() limit 1`。合法路线是走 GitHub 开源库 + 保留来源和许可证声明, 不是爬 caonima.de。
- README 里写的另一个接口 `nmsl.shadiao.app/api.php` 已经挂了, 实测重定向到 GitHub, 不能当运行时接口用。所以这里做**离线打包导入**, 不做运行时联网拉取。

### 13.2 筛了多少, 为什么只筛 min

原库 1679 条: min 口吐芬芳 682 条, max 火力全开 997 条。筛选手规则 (见 `tools/import_zuanbot.py`, 可重新跑):

- 只取每条第一行 (原库很多是多行段子, LoL 聊天框是单行), 长度 5~80 字。
- 过 `SwearObfuscator` 同款映射; 过完不能再有原形脏话和烧掉的写法; 不能含 Obfuscator 覆盖不了的露骨词 (逼/屄/阴道/肛门/强奸/杂种/卖逼/鸡巴/口交...)。
- 去重, 去掉跟现有 RoastTemplates 重复的行。
- **只筛 min, 保留 655 条**, 写进 `CanyonEcho_Wpf/Assets/zuanbotLibrary.json` (66KB, 来源和许可证写在文件头里)。
- **max 没打包**: 997 条里就算筛完也还剩 600 多条, 但大量是性/生殖器/杀全家/灵车骨灰内容, 就算把脏话字换掉, 语义本身也是 Riot 26.5 要封号的 "严重辱骂", 打包进来等于给用户埋雷。想自己筛 max 的跑 `python3 tools/import_zuanbot.py --include-max`, 后果自担。
- 原库 `db/data.db` (970KB) 没进仓库, 脚本用时自动从 GitHub 下载; `tools/import_zuanbot.py` 里规则都写清楚了。

### 13.3 怎么接进程序的

- `CanyonEcho_Wpf/Assets/zuanbotLibrary.json` 已在 csproj 里标成 EmbeddedResource, 跟 `wordsLibrary.json` 同款加载方式 (`ManifestHelper.GetManifestStream`)。
- 新增 `Helper/ZuanbotLibrary.cs`: 懒加载, 随机抽一条, 最近 15 条不重复 (跟 `WordsLibrary` 同款逻辑), 长度超过 80 字的不进, 加载失败返回 null 不崩, 调用方用内置模板兜底。
- `BuiltInSnarkyEngine.GenerateReply`: Roast 模式下 25% 概率从外站词库抽一条, 75% 还是走内置 RoastTemplates (免得外站语录把意图分类冲没了)。外站词库导入时已经过隐形映射, 发送时 `GameSendHelper` 还会再隐形一次。
