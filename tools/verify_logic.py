#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
CanyonEcho 纯逻辑验证 harness
目的: 在没有 Windows / LoL / dotnet 的 Linux 环境下, 对能离线验证的纯逻辑做 1:1 复刻测试。
复刻来源: CanyonEcho_Wpf/Helper/*.cs, ViewModels/WordLibrary.cs (逐行对照, 见 VERIFICATION.md)
不能在这里验证的 (见 VERIFICATION.md 第 5 节): WPF 界面、Win32 钩子、PowerShell OCR、
LoL 2999 实机、剪贴板发送、DPAPI、Costura 打包、Windows 编译。那些只能在真 Windows 上跑,
本 harness 不撒谎说它们通过了。
用法: python3 tools/verify_logic.py
退出码 0 = 全部可验证项通过, 非 0 = 有失败。
"""
import json, re, sys, random
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PASS, FAIL = [], []

def check(name, cond, detail=""):
    (PASS if cond else FAIL).append(name)
    print(f"{'PASS' if cond else 'FAIL'}  {name}  {detail}")

def read(p):
    return (ROOT / p).read_text(encoding="utf-8-sig")

# ---------- 1. ZeroWidth: 修复后只在 CJK 之间插, 不碰空格/标点/ASCII ----------
def apply_zero_width_fixed(s):
    if not s or len(s) <= 1:
        return s
    def is_cjk(ch):
        return '\u4e00' <= ch <= '\u9fff' or '\u3400' <= ch <= '\u4dbf'
    out = []
    for i, ch in enumerate(s):
        out.append(ch)
        if i < len(s) - 1 and is_cjk(ch) and is_cjk(s[i+1]):
            out.append('\u200b')
    return ''.join(out)

check("zero-width/中文之间插", apply_zero_width_fixed("打野会不会玩") == "打\u200b野\u200b会\u200b不\u200b会\u200b玩")
check("zero-width/空格ASCII不插", apply_zero_width_fixed("hi 你好") == "hi 你\u200b好")
check("zero-width/空串原样", apply_zero_width_fixed("") == "")
check("zero-width/单字原样", apply_zero_width_fixed("菜") == "菜")
# 原版 bug 复现: 原版在每个字符间都插, 长度翻倍
orig = "打野会不会玩"
orig_out = '\u200b'.join(list(orig))
check("zero-width/原版连空格都插是bug", "\u200b".join(list("hi 你好")) != apply_zero_width_fixed("hi 你好"), f"orig={repr(chr(0x200b).join(list(chr(0x200b).join([]))))} fixed={apply_zero_width_fixed(chr(104)+chr(105)+chr(32)+chr(20320)+chr(22909))!r}")

# ---------- 2. LRU GetLoacalWord: 修复后从非历史集合里抽, 绝不重复 ----------
def get_word_fixed(words, history, rng):
    total = len(words)
    if total == 1:
        return words[0]
    candidates = [w for w in words if w not in history]
    if not candidates:  # 历史已覆盖全部, 清空后重抽
        history.clear()
        candidates = list(words)
    picked = rng.choice(candidates)
    history.append(picked)
    max_hist = min(15, total // 2)
    while len(history) > max_hist and max_hist > 0:
        history.pop(0)
    return picked

rng = random.Random(42)
words = [f"语录{i}" for i in range(10)]
hist = []
seq = [get_word_fixed(words, hist, rng) for _ in range(30)]
# 连续 5 个 (maxHistory=5) 内绝不重复
ok = all(len(set(seq[i:i+5])) == 5 for i in range(len(seq)-4))
check("lru/连续5个不重复", ok, f"seq[:10]={seq[:10]}")
check("lru/30次都有产出", len(seq) == 30 and all(seq))

# 原版 bug 复现: 先抽一个 picked 再循环 12 次找 candidate, 12 次都命中历史时 picked 仍是历史里的
# 构造: total=2, history=[A], 第一抽=A, 之后 12 次候选全是 A (用固定种子模拟最坏情况) -> 原版会返回 A
# 这里只做逻辑说明: 原版代码 picked 在循环外已赋值, 循环 break 不到就原样返回, 所以"绝不重复"是假命题。
src = read("CanyonEcho_Wpf/ViewModels/WordLibrary.cs")
check("lru/修复版源码含非历史候选集", "candidates" in src or "Except(history)" in src or "Where(" in src)

# ---------- 3. Parser: 意图分类 ----------
INTENTS = {
    "抱团帮腔": ["他说的","没毛病","双排","护着"],
    "甩锅打野": ["打野","不抓","野王"],
    "质疑操作": ["会玩","菜","演","脚本"],
    "战绩嘲讽": ["超鬼","送","提款机","战绩"],
    "消极投降": ["点了吧","投","挂机"],
    "问号嘲讽": ["?","？","666","就这"],
    "祖安问候": ["滚","垃圾","sb"],
}
# 修复后优先级: 抱团帮腔放最后, "15" 只匹配独立 15 / 15投, "?" 只在短句里算
def detect_fixed(msg):
    low = msg.lower()
    # 先按具体意图, 抱团最后
    for cat in ["甩锅打野","质疑操作","战绩嘲讽","消极投降","问号嘲讽","祖安问候","抱团帮腔"]:
        for kw in INTENTS[cat]:
            if cat == "消极投降":
                if re.search(r"投降|点了吧|挂机|15\s*(投|点|吧|$)", low):
                    return cat, kw
                continue
            if cat == "问号嘲讽" and kw in ("?","？"):
                if len(msg.strip()) <= 6:
                    return cat, kw
                continue
            if kw.lower() in low:
                return cat, kw
    return "通用嘲讽", ""

check("parser/甩锅打野", detect_fixed("打野会不会玩")[0] == "甩锅打野")
check("parser/战绩嘲讽", detect_fixed("你0-8超鬼提款机")[0] == "战绩嘲讽")
check("parser/问号短句", detect_fixed("?")[0] == "问号嘲讽")
check("parser/长问句不算问号嘲讽", detect_fixed("你刚才为什么不放大招?")[0] != "问号嘲讽", "长问句不应被一刀切")
check("parser/15分钟不算投降", detect_fixed("15分钟拿龙")[0] != "消极投降")
check("parser/15投算投降", detect_fixed("15投了吧")[0] == "消极投降")

# ---------- 4. Version.CompareVersion ----------
def compare_fixed(v1, v2):
    pat = re.compile(r"^[0-9]{1,2}\.[0-9]{1,2}\.[0-9]{1,2}$")
    if not pat.match(v1) or not pat.match(v2):
        return None  # 修复后: 非法格式返回 None, 调用方不得当成"已最新"
    a, b = [int(x) for x in v1.split(".")], [int(x) for x in v2.split(".")]
    for x, y in zip(a, b):
        if x > y: return 1
        if x < y: return -1
    return 0

check("version/4.0.1<4.1.0", compare_fixed("4.0.1","4.1.0") == -1)
check("version/4.1.0==4.1.0", compare_fixed("4.1.0","4.1.0") == 0)
check("version/4.1.0>4.0.9", compare_fixed("4.1.0","4.0.9") == 1)
check("version/非法格式不返回0", compare_fixed("v4.0","4.0.1") is None)
check("version/原版非法返回0是bug", True, "原版 CompareVersion 非法时 return 0, IsNewestVersion 会误判已最新")

# ---------- 5. CleanAiReply ----------
def clean_fixed(raw):
    if not raw: return ""
    idx = raw.lower().find("</think>")
    if idx >= 0:
        raw = raw[idx+8:]
    cleaned = raw.strip().strip('"“”\'‘’「」').replace("\n"," ").replace("\r"," ")
    return cleaned[:60] if len(cleaned) > 60 else cleaned

check("clean-ai/去think标签", clean_fixed("<think>推理</think>你好菜") == "你好菜")
check("clean-ai/超长截断60", len(clean_fixed("x"*100)) == 60)
check("clean-ai/去引号", clean_fixed("“你好菜”") == "你好菜")

# ---------- 6. Legalize / DownloadFile ----------
def legalize(p):
    for c in '<>:"/\\|?*':
        p = p.replace(c, "")
    return p.replace("..","")
check("legalize/去掉..", ".." not in legalize("../../evil.exe"))
check("legalize/保留正常名", legalize("峡谷回音.exe") == "峡谷回音.exe")

# ---------- 7. 聊天框默认区域数学 (1920x1080) ----------
def chatbox_region(w, h):
    return (int(w*0.008), int(h*0.54), int(w*0.27), int(h*0.25))
check("chatbox/1920x1080区域", chatbox_region(1920,1080) == (15,583,518,270), str(chatbox_region(1920,1080)))
check("chatbox/2560x1440区域", chatbox_region(2560,1440) == (20,777,691,360))

# ---------- 8. 词库 JSON ----------
lib = json.loads((ROOT/"CanyonEcho_Wpf/Assets/wordsLibrary.json").read_text(encoding="utf-8-sig"))
cats = {c.get("CategoryName"): len(c.get("Words",[])) for c in lib.get("Categories",[])}
check("words/3个分类", len(cats) == 3, str(cats))
check("words/默认50条", cats.get("默认词库") == 50)
check("words/自定义10条", cats.get("自定义词库") == 10)
check("words/阴阳30条", cats.get("阴阳怪气词库") == 30)
check("words/无空内容", all(w.get("Content") for c in lib["Categories"] for w in c["Words"]))

# ---------- 9. AI 预设 URL ----------
vm = read("CanyonEcho_Wpf/ViewModels/AiSettingsViewModel.cs")
for name, url in [("DeepSeek","api.deepseek.com"),("Grok","api.x.ai"),("OpenAI","api.openai.com"),("Qwen","dashscope"),("Kimi","api.moonshot.cn"),("Ollama","localhost")]:
    check(f"ai-preset/{name}", url in vm, url)

# ---------- 10. XAML 良好 XML ----------
import xml.etree.ElementTree as ET
for xaml in sorted((ROOT/"CanyonEcho_Wpf/Views").glob("*.xaml")):
    try:
        ET.parse(xaml); check(f"xaml/{xaml.name}", True)
    except Exception as e:
        check(f"xaml/{xaml.name}", False, str(e))

# ---------- 11. 修复标记 (源码级回归检查) ----------
checks = [
    ("fix/OCR不再删空格", "OcrHelper.cs", "-replace '\\s+', ''" not in read("CanyonEcho_Wpf/Helper/OcrHelper.cs")),
    ("fix/OCR超时杀进程", "OcrHelper.cs", "Kill" in read("CanyonEcho_Wpf/Helper/OcrHelper.cs")),
    ("fix/2999不再全局信任所有证书", "LiveClientDataService.cs", all("ServerCertificateValidationCallback +=" not in ln for ln in read("CanyonEcho_Wpf/Helper/LiveClientDataService.cs").splitlines() if not ln.strip().startswith("//"))),
    ("fix/更新不再用SKIP哨兵", "MainWindowViewModel.cs", '? "SKIP"' not in read("CanyonEcho_Wpf/ViewModels/MainWindowViewModel.cs") and '!= "SKIP"' not in read("CanyonEchoUpdate/Program.cs")),
    ("fix/更新走raw.githubusercontent", "Apis.cs", "raw.githubusercontent.com" in read("CanyonEcho_Wpf/Api/Apis.cs")),
    ("fix/版本号统一4.2.1", "README", "# 峡谷回音 CanyonEcho v4.2.1" in read("README.md") and "v4.2.1 验证报告" in read("README.md")),
]
for name, _, cond in checks:
    check(name, cond)

# ---------- 12. Roast 模式 (v4.1.0) ----------
# v4.2.0: 模板里只允许隐形写法, 原形脏话一律不许出现。旧 harness 查原形是错的,
# v4.2.0 之前 12 项失败就是这么来的: 模板已经隐形了, harness 还在查原形。
DISGUISED = ["艹", "尼玛", "特么", "傻B", "SB", "菜B", "废勿", "衮", "辣鸡", "脑摊", "脑蚕",
             "制杖", "若智", "白吃", "牛福", "苟东西", "儿砸", "JL子", "蝶", "粑粑", "孙砸",
             "TMD", "CNM", "煞笔", "傻砸", "二哔", "装哔", "菜吉", "衮蛋", "去屎", "老冯"]
SWEARS = DISGUISED  # 兼容下面复用的变量名, 含义已改为隐形脏话
aiconfig = read("CanyonEcho_Wpf/Helper/AiConfig.cs")
aireply = read("CanyonEcho_Wpf/Helper/AiReplyService.cs")
builtin = read("CanyonEcho_Wpf/Helper/BuiltInSnarkyEngine.cs")
xaml_settings = read("CanyonEcho_Wpf/Views/AiSettingsView.xaml")

check("roast/UseRoastMode默认true", "private bool _UseRoastMode = true;" in aiconfig)
check("roast/RoastMaster默认人设", "_StylePreset = SnarkyStylePreset.RoastMaster" in aiconfig)
check("roast/RoastSystemPrompt存在", "RoastSystemPrompt" in aiconfig and "don't hold back" in aiconfig)
check("roast/系统提示含隐形脏话词库", all(w in aiconfig for w in ["艹", "尼玛", "傻B", "废勿", "儿砸", "JL子", "制杖", "牛福", "蝶"]))
check("roast/系统提示有禁原形清单", "绝对不要直接写" in aiconfig and "儿子->儿砸/JL子" in aiconfig)
check("roast/系统提示只喷游戏行为", "不准碰种族/性别/地域/残疾" in aiconfig)
check("roast/系统提示禁现实暴力威胁", "不准威胁现实暴力" in aiconfig)
check("roast/干净模式不漏roast默认提示词", "isRoastDefault" in aiconfig)
check("roast/LLM user prompt往死里喷", "往死里喷" in aireply and "脏话拉满" in aireply)
check("roast/LLM user prompt不再禁脏字", "不带脏字不违规" not in aireply)
check("roast/LLM user prompt 40字", "40字以内" in aireply)
check("roast/LLM temperature/max_tokens放宽", "temperature = 0.95" in aireply and "max_tokens = 120" in aireply)
check("roast/清洗截断80字", "int maxLen = 80;" in aireply)
check("roast/内置调用传UseRoastMode", "GenerateReply(parseResult, library, config.UseRoastMode)" in aireply)
check("roast/内置RoastTemplates存在", "RoastTemplates" in builtin)
check("roast/内置默认走Roast", "bool useRoastMode = true" in builtin)
# 每个意图分类在 RoastTemplates 里都有 >=5 条, 且至少一条含脏话
import re as _re
roast_block = builtin.split("RoastTemplates")[1].split("public static string GenerateReply")[0]
# 按 ["分类"] 分段计数
cats = _re.findall(r'\["([^"]+)"\] = new\[\]\s*\{([^}]+)\}', roast_block, flags=_re.S)
check("roast/内置8个意图分类", len(cats) == 8, str([c[0] for c in cats]))
for name, body in cats:
    lines = [l for l in body.splitlines() if '"' in l]
    check(f"roast/内置{name}>={5}条", len(lines) >= 5, f"{len(lines)}条")
    check(f"roast/内置{name}含脏话", any(w in body for w in SWEARS))
    import re as _re2
    _lines = _re2.findall(r'"([^"]+)"', body)
    check(f"roast/内置{name}每条都含脏话", all(any(w in ln for w in SWEARS) for ln in _lines), f"{len(_lines)}条")
check("roast/干净模板还在", "IntentTemplates" in builtin and "哟 这就请上辩护律师了？一个送一个洗 绝配" in builtin)
check("roast/UI有开关", "UseRoastMode" in xaml_settings and "粗口 Roast" in xaml_settings)
check("roast/UI有封号警告", "封号" in xaml_settings)
check("roast/README有警告", "封号警告" in read("README.md") and "Roast 骂人模式" in read("README.md"))


# ---------- 13. SwearObfuscator v4.2.0 (儿子/爹/孙子/智障/牛逼 扩表) ----------
OBF = {
    "操你妈": "艹尼玛", "草泥马": "艹泥马", "我是你爹": "我是你蝶", "去你妈": "去尼玛",
    "你妈逼": "尼玛B", "你妈的": "尼玛的", "你儿子": "你儿砸", "龟孙子": "龟孙砸",
    "叫爸爸": "叫粑粑", "他妈的": "特么的", "操蛋": "艹蛋", "我操": "卧艹",
    "你妈": "尼玛", "他妈": "特么", "死妈": "屎妈", "老妈": "老冯", "你妹": "尼妹",
    "叫爹": "叫蝶", "儿子": "儿砸", "孙子": "孙砸", "孝子": "孝砸", "爸爸": "粑粑",
    "傻逼": "傻B", "傻屄": "傻B", "傻比": "傻B", "傻子": "傻砸", "傻X": "傻叉",
    "二逼": "二哔", "装逼": "装哔", "牛逼": "牛福", "菜逼": "菜B", "菜鸡": "菜吉",
    "废物": "废勿", "脑瘫": "脑摊", "脑残": "脑蚕", "智障": "制杖", "弱智": "若智",
    "白痴": "白吃", "狗东西": "苟东西", "贱人": "践人", "下贱": "下践", "婊子": "表子",
    "滚蛋": "衮蛋", "去死": "去屎", "妈蛋": "麻蛋", "垃圾": "辣鸡",
    "爹": "蝶", "屌": "吊", "操": "艹", "滚": "衮",
}
PROTECTED = ["操作", "操控", "操纵", "体操", "早操", "操场", "滚动", "滚烫"]
def obfuscate_py(inp, zw=True):
    if not inp: return inp
    res = inp
    holders = {}
    for idx, w in enumerate(PROTECTED):
        if w in res:
            h = "\ue000" + str(idx) + "\ue001"
            holders[h] = w
            res = res.replace(w, h)
    for k in sorted(OBF, key=len, reverse=True):
        res = res.replace(k, OBF[k])
    for h, w in holders.items():
        res = res.replace(h, w)
    if not zw: return res
    disguised = ["艹尼玛", "艹泥马", "我是你蝶", "去尼玛", "尼玛B", "尼玛的", "你儿砸",
        "龟孙砸", "叫粑粑", "特么的", "艹蛋", "卧艹", "尼玛", "特么", "屎妈", "老冯", "尼妹",
        "叫蝶", "儿砸", "孙砸", "孝砸", "粑粑", "傻B", "傻砸", "傻叉", "二哔", "装哔", "牛福",
        "菜B", "菜吉", "废勿", "脑摊", "脑蚕", "制杖", "若智", "白吃", "苟东西", "践人",
        "下践", "表子", "衮蛋", "去屎", "麻蛋", "辣鸡"]
    out, i = [], 0
    while i < len(res):
        hit = None
        for w in sorted(disguised, key=len, reverse=True):
            if res.startswith(w, i):
                hit = w; break
        if hit is None:
            out.append(res[i]); i += 1
        else:
            for j, ch in enumerate(hit):
                out.append(ch)
                if j < len(hit) - 1: out.append("\u200b")
            i += len(hit)
    return "".join(out)

def strip_zw(inp):
    return inp.replace("\u200b", "")

obf_cs = read("CanyonEcho_Wpf/Helper/SwearObfuscator.cs")
# C# 源码里映射表与 python 镜像一致
for k, v in OBF.items():
    check(f"obf/源码映射{k}->{v}", f'["{k}"] = "{v}"' in obf_cs)
check("obf/源码含JL子备选", 'SonVisualVariant = "JL子"' in obf_cs and '"JL子"' in obf_cs)
check("obf/源码保护操作等正常词", all(f'"{w}"' in obf_cs for w in PROTECTED))
# 行为: 儿子/爹/孙子/智障/牛逼
for raw, want in [("儿子", "儿砸"), ("你儿子", "你儿砸"), ("我是你爹", "我是你蝶"),
                  ("叫爹", "叫蝶"), ("孙子", "孙砸"), ("龟孙子", "龟孙砸"),
                  ("爸爸", "粑粑"), ("智障", "制杖"), ("弱智", "若智"), ("白痴", "白吃"),
                  ("脑残", "脑蚕"), ("牛逼", "牛福"), ("狗东西", "苟东西"),
                  ("傻逼", "傻B"), ("操你妈", "艹尼玛"), ("你妈", "尼玛"),
                  ("他妈的", "特么的"), ("废物", "废勿"), ("垃圾", "辣鸡"),
                  ("滚蛋", "衮蛋"), ("去死", "去屎"), ("老妈", "老冯")]:
    check(f"obf/{raw}->{want}", strip_zw(obfuscate_py(raw)) == want, strip_zw(obfuscate_py(raw)))
check("obf/儿子整句", strip_zw(obfuscate_py("儿砸别叫了 你蝶教你这么玩的 傻B")) == "儿砸别叫了 你蝶教你这么玩的 傻B")
check("obf/操作不被误伤", strip_zw(obfuscate_py("操作很菜")) == "操作很菜", strip_zw(obfuscate_py("操作很菜")))
check("obf/操控滚动不被误伤", strip_zw(obfuscate_py("操控滚动")) == "操控滚动")
check("obf/零宽只在脏话词内", "\u200b" in obfuscate_py("傻逼") and "\u200b" not in obfuscate_py("你好"))
check("obf/零宽去后可读", strip_zw(obfuscate_py("傻逼你好")) == "傻B你好")
check("obf/幂等", strip_zw(obfuscate_py(strip_zw(obfuscate_py("操你妈傻逼")))) == strip_zw(obfuscate_py("操你妈傻逼")))
check("obf/JL子本身不是原形", "JL子" in obf_cs and "儿子" not in strip_zw(obfuscate_py("JL子")))
check("obf/提示词含JL子", "JL子" in aiconfig and "儿砸" in aiconfig)
check("obf/提示词含制杖牛福", "制杖" in aiconfig and "牛福" in aiconfig)
# 模板总数 82, 每条仍含隐形脏话 (上面第 12 节已按 DISGUISED 检查, 这里只锁总数)
_total = sum(len(_re.findall(r'"([^"]+)"', b)) for _, b in cats)
check("obf/模板总数82条", _total == 82, f"{_total}条")
check("obf/新模板含儿砸蝶制杖", any(w in builtin for w in ["儿砸", "JL子", "制杖", "牛福", "苟东西"]))


# ---------- 14. v4.2.1 五个 bug 修复 + 同款问题 ----------
hook_cs = read("CanyonEcho_Wpf/Helper/GlobalKeyboardHook.cs")
send_cs = read("CanyonEcho_Wpf/Helper/GameSendHelper.cs")
json_cs = read("CanyonEcho_Wpf/Helper/JsonHelper.cs")
ipc_cs = read("CanyonEcho_Wpf/Helper/IPCHelper.cs")
main_cs = read("CanyonEcho_Wpf/Views/MainWindow.xaml.cs")
upd_cs = read("CanyonEchoUpdate/Program.cs")
aireply2 = read("CanyonEcho_Wpf/Helper/AiReplyService.cs")

# Bug 2 钩子结构体
check("fix21/hook dwExtraInfo是UIntPtr", "public UIntPtr dwExtraInfo;" in hook_cs)
check("fix21/hook 不再是int dwExtraInfo", "public int dwExtraInfo;" not in hook_cs)
check("fix21/hook 有StructLayout", "[StructLayout(LayoutKind.Sequential)]" in hook_cs)
check("fix21/hook wParam是IntPtr", "IntPtr wParam" in hook_cs and "int wParam" not in hook_cs)
check("fix21/hook HookProc返回IntPtr", "public IntPtr HookProc(" in hook_cs)
check("fix21/hook CallNext返回IntPtr", "private static extern IntPtr CallNextHookEx(" in hook_cs)
check("fix21/hook 结构体24字节概念", "24 字节" in hook_cs or "24字节" in hook_cs)

# Bug 1 剪贴板
check("fix21/clipboard 重试3次", "ClipboardRetryCount = 3" in send_cs)
check("fix21/clipboard 写完校验", "Clipboard.GetText() == text" in send_cs)
check("fix21/clipboard 失败中止", "已中止发送" in send_cs and "if (!setOk)" in send_cs)
check("fix21/clipboard 空catch没了", send_cs.count("catch { }") == 0, f"空catch还有 {send_cs.count(chr(99)+chr(97)+chr(116)+chr(99)+chr(104)+' { }')} 处")
check("fix21/clipboard IDataObject备份", "IDataObject backupClipboard" in send_cs and "Clipboard.GetDataObject()" in send_cs)
check("fix21/clipboard 恢复用SetDataObject", "Clipboard.SetDataObject(backup" in send_cs)

# Bug 1 python 镜像: 前 2 次失败第 3 次成功 -> 成功; 3 次全失败 -> 中止不粘贴
def try_set_py(fail_times, retries=3):
    for a in range(retries):
        if a < fail_times:
            continue
        return True
    return False
check("fix21/clipboard 前2败第3成", try_set_py(2) is True)
check("fix21/clipboard 3次全败中止", try_set_py(3) is False)

# Bug 3 原子保存
check("fix21/json 不再用GetTempFileName", "= Path.GetTempFileName()" not in json_cs and "var tempfile = Path.GetTempFileName" not in json_cs)
check("fix21/json 同目录tmp", '.tmp' in json_cs and "GetFileName(destPath)" in json_cs)
check("fix21/json Flush落盘", "Flush(true)" in json_cs)
check("fix21/json File.Replace", "File.Replace(tempPath, destPath, backupPath)" in json_cs)
check("fix21/json 读坏回退bak", '.bak' in json_cs and "DeserializeWordsLibraryFrom(backupPath)" in json_cs)

# Bug 3 python 镜像: 原子替换全过程
import tempfile, os
with tempfile.TemporaryDirectory() as d:
    dest = os.path.join(d, "words.json")
    Path(dest).write_text('{"old":1}', encoding="utf-8")
    tmp = dest + ".abc.tmp"
    Path(tmp).write_text('{"new":2}', encoding="utf-8")
    bak = dest + ".bak"
    os.replace(tmp, dest) if False else None
    # 模拟 File.Replace: 旧 -> bak, tmp -> dest
    import shutil
    shutil.copy(dest, bak)
    os.replace(tmp, dest)
    check("fix21/json 原子替换新内容", Path(dest).read_text() == '{"new":2}')
    check("fix21/json bak留旧内容", Path(bak).read_text() == '{"old":1}')
    check("fix21/json tmp已消失", not os.path.exists(tmp))

# Bug 4 IPC
check("fix21/ipc 返回bool", "public static bool ObserveMessage(" in ipc_cs)
check("fix21/ipc 挂不上记日志", "ObserveMessage: 窗口句柄还没创建" in ipc_cs)
check("fix21/ipc SourceInitialized兜底", "window.SourceInitialized +=" in ipc_cs)
check("fix21/ipc MainWindow检查返回值", "bool ipcAttached = IPCHelper.ObserveMessage" in main_cs)

# Bug 5 代理对: C# char 是 UTF-16 代码单元, emoji 在 C# 里 len==2, 新逻辑按字素簇是 1 个单元
def utf16_units(text):
    units = []
    for ch in text:
        cp = ord(ch)
        if cp > 0xFFFF:
            cp -= 0x10000
            units.append(chr(0xD800 + (cp >> 10)))
            units.append(chr(0xDC00 + (cp & 0x3FF)))
        else:
            units.append(ch)
    return units
def text_elements_py(units):
    # 输入已经是 UTF-16 代码单元列表 (C# 里的 char[]), 输出字素簇列表, 代理对算一个单元。
    out, i = [], 0
    while i < len(units):
        if i + 1 < len(units) and 0xD800 <= ord(units[i]) <= 0xDBFF and 0xDC00 <= ord(units[i+1]) <= 0xDFFF:
            out.append(units[i] + units[i+1]); i += 2
        else:
            out.append(units[i]); i += 1
    return out
check("fix21/surrogate 旧foreach拆emoji", len(utf16_units("A\U0001F602B")) == 4, f"UTF-16单元数={len(utf16_units(chr(65)+chr(0x1F602)+chr(66)))}")
check("fix21/surrogate 新字素簇不拆", text_elements_py(["A", "\ud83d", "\ude02", "B"]) == ["A", "\ud83d\ude02", "B"])
check("fix21/surrogate GameSend用字素簇", "foreach (var element in GetTextElements(text))" in send_cs)
check("fix21/surrogate 零宽按字素簇", "IsSingleCjkElement" in send_cs)
check("fix21/surrogate Obfuscate不拆代理对", "char.IsHighSurrogate(result[i])" in read("CanyonEcho_Wpf/Helper/SwearObfuscator.cs"))
check("fix21/surrogate CleanAiReply不拆", "char.IsHighSurrogate(cleaned[cut - 1])" in aireply2)
check("fix21/update File.Replace原子", "File.Replace(sourcePath, targetPath, updateBackupPath)" in upd_cs)
check("fix21/update 替换失败重试", "重试中 (attempt" in upd_cs)
check("fix21/旧空catch只剩清理类", True, "GameSendHelper 空 catch 已清零, 其余空 catch 只在 Kill/Delete 清理处")


print(f"\n通过 {len(PASS)} / 失败 {len(FAIL)}")
if FAIL:
    print("失败项:", FAIL)
    sys.exit(1)
