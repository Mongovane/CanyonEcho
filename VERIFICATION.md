# CanyonEcho v4.2.0 — 验证报告 (VERIFICATION)

> 规矩先说死, 别他妈再问 "能不能直接用": **这份报告里写通过的, 才是真的跑过测试的;
> 写 "未验证 / 需 Windows 实机" 的, 就是没跑过, 谁吹通过谁傻逼。**
> 验证时间: 2026-11-12, 环境: Linux VM, 无 dotnet / 无 Windows / 无 LoL / 无真机。
> 原仓库: https://github.com/Mongovane/CanyonEcho.git , commit `bef0085`。
> 改进版: 本目录, 版本统一为 **4.2.0** (v4.0.1 的 7 个 bug 修复 + v4.1.0 粗口 Roast 模式 + v4.2.0 脏话隐形扩表)。
> v4.2.0 改动: `SwearObfuscator` 12 个映射 -> 50 个, `RoastTemplates` 61 条 -> 82 条, `RoastSystemPrompt` / Custom 提示词加入儿砸/JL子/制杖/牛福等新隐形词, harness 179/179 通过。

## 1. 怎么验证的

- 纯逻辑: `tools/verify_logic.py`, **179 项**, 1:1 复刻 C# 算法再跑, 结果 **179/179 通过**。
  跑法: `python3 tools/verify_logic.py`, 退出码 0。
  其中 v4.0.1 的 49 项 (修复前 42/49, 7 项失败正好是修掉的 7 个 bug) + v4.1.0 Roast 模式新增 44 项 + v4.2.0 隐形扩表新增 86 项, 全部通过。
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
- 现在: 全部统一 **4.2.0** (`README.md`, `AssemblyInfo.cs`, `一键编译生成EXE.bat`, `version.json` 的 `VersionName`)。v4.1.0 时期的版本号已作为历史写在第 8-9 节里, 不再是当前版本。

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
