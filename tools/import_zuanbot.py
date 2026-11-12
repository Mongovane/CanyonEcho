#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
从 cndiandian/zuanbot.com (Apache-2.0) 的 SQLite 词库里筛出能进 LoL 聊天框的祖安语录,
生成 CanyonEcho_Wpf/Assets/zuanbotLibrary.json。

为什么不用 caonima.de:
- caonima.de 没有公开 API, 只有同站 ./api.php, 首页注释明确写了 "请勿未经许可
  抄袭本站代码, 我可以提供给你, 但前提是你好好跟我说"。
- 同一份词库的源头在 GitHub 开源了: cndiandian/zuanbot.com, Apache-2.0,
  db/data.db (SQLite, main 表: id, text, level), api.php 只有一行
  SELECT text FROM main WHERE LEVEL='min'|'max' ORDER BY RANDOM() limit 1。
  所以合法路线是走 GitHub 开源库 + 按 Apache-2.0 保留来源和许可证声明,
  不是去爬 caonima.de。
- nmsl.shadiao.app 的 api.php (README 里写的那个) 已经挂了, 实测重定向到 GitHub,
  不能当运行时接口用。所以这里做离线打包导入, 不做运行时联网拉取。

筛选手规则 (只进 min 口吐芬芳, 不要 max 火力全开):
- max 里大量是性/生殖器/杀全家/灵车骨灰这类内容, 就算把脏话字换掉, 语义本身
  也是 Riot 26.5 要封号的 "严重辱骂" (26.5 起严重辱骂直接封号, 不只是禁言),
  而且举报一投一个准。min 是轻度阴阳怪气, 跟现有 RoastTemplates 一个级别。
- 只取每条的第一行 (原库很多是多行段子, LoL 聊天框是单行)。
- 长度 5~80 字 (CleanAiReply 截断 80 字, 发送方也按 80 字算)。
- 过 SwearObfuscator 同款映射 (python 镜像, 见 tools/verify_logic.py 第 13 节)。
- 过完之后不能再有原形脏话 (傻逼/你妈/他妈/操你妈/废物/垃圾/智障/牛逼/儿子/爹)
  和烧掉的隐形写法 (傻B/SB/TMD/卧艹/卧槽/去屎)。
- 不能含 Obfuscator 覆盖不了的露骨词 (逼/屄/阴道/肛门/强奸/杂种/卖逼/鸡巴/口交...)。
- 去重, 去掉跟现有 RoastTemplates 重复的行。

用法:
  python3 tools/import_zuanbot.py [--db /path/to/data.db] [--include-max]
  默认只筛 min。--include-max 会把 max 也按同样规则筛进来 (不推荐, 见上)。
  不带 --db 时自动从 GitHub raw 下载 db 到 tools/zuanbot.db (缓存)。
"""
import argparse, json, sqlite3, sys, urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DB_URL = "https://raw.githubusercontent.com/cndiandian/zuanbot.com/master/db/data.db"
OUT = ROOT / "CanyonEcho_Wpf/Assets/zuanbotLibrary.json"

OBF = {"操你妈": "艹尼玛", "草泥马": "艹泥马", "我是你爹": "我是你蝶", "去你妈": "去尼玛",
       "你妈逼": "尼玛B", "你妈的": "尼玛的", "你儿子": "你儿砸", "龟孙子": "龟孙砸",
       "叫爸爸": "叫粑粑", "他妈的": "特么的", "操蛋": "艹蛋", "我操": "艹",
       "你妈": "尼玛", "他妈": "特么", "死妈": "屎妈", "老妈": "老冯", "你妹": "尼妹",
       "叫爹": "叫蝶", "儿子": "儿砸", "孙子": "孙砸", "孝子": "孝砸", "爸爸": "粑粑",
       "傻逼": "傻哔", "傻屄": "傻哔", "傻比": "傻哔", "傻子": "傻砸", "傻X": "傻叉",
       "二逼": "二哔", "装逼": "装哔", "牛逼": "牛福", "菜逼": "菜B", "菜鸡": "菜吉",
       "废物": "废勿", "脑瘫": "脑摊", "脑残": "脑蚕", "智障": "制杖", "弱智": "若智",
       "白痴": "白吃", "狗东西": "苟东西", "贱人": "践人", "下贱": "下践", "婊子": "表子",
       "滚蛋": "衮蛋", "去死": "去似", "妈蛋": "麻蛋", "垃圾": "辣鸡",
       "爹": "蝶", "屌": "吊", "操": "艹", "滚": "衮",
       "傻B": "傻哔", "SB": "煞笔", "TMD": "特么", "卧艹": "艹", "卧槽": "艹", "去屎": "去似"}
PROTECTED = ["操作", "操控", "操纵", "体操", "早操", "操场", "滚动", "滚烫"]
BLOCK = ["逼", "屄", "屌", "阴道", "阴茎", "肛门", "强奸", "杂种", "卖逼", "卖淫", "嫖",
         "鸡巴", "做爱", "性交", "轮奸", "精液", "睾丸", "乳头", "屁眼", "菊花", "插逼",
         "捅", "奸", "肏", "做鸡", "处女", "破处", "内射", "口交", "舔", "吃屎", "喝尿"]
FORBIDDEN_AFTER = ["傻B", "SB", "TMD", "卧艹", "卧槽", "去屎",
                   "傻逼", "你妈", "他妈", "操你妈", "废物", "垃圾", "智障", "牛逼", "儿子", "爹"]


def obfuscate(inp):
    res = inp
    for idx, w in enumerate(PROTECTED):
        res = res.replace(w, f"\ue000{idx}\ue001")
    for k in sorted(OBF, key=len, reverse=True):
        res = res.replace(k, OBF[k])
    for idx, w in enumerate(PROTECTED):
        res = res.replace(f"\ue000{idx}\ue001", w)
    return res.replace("\u200b", "")


def survives(text):
    lines = [ln.strip() for ln in text.split("\n") if ln.strip()]
    if not lines:
        return None
    first = lines[0]
    if len(first) > 80 or len(first) < 5:
        return None
    out = obfuscate(first)
    if any(b in out for b in BLOCK):
        return None
    if any(w in out for w in FORBIDDEN_AFTER):
        return None
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--db", default=str(ROOT / "tools/zuanbot.db"))
    ap.add_argument("--include-max", action="store_true",
                    help="把 max 火力全开也筛进来 (不推荐: 大量严重辱骂内容, 见文件头注释)")
    args = ap.parse_args()

    db_path = Path(args.db)
    if not db_path.exists():
        print(f"下载词库 db: {DB_URL}")
        urllib.request.urlretrieve(DB_URL, db_path)
    con = sqlite3.connect(db_path)
    rows = con.execute("SELECT text, level FROM main").fetchall()
    levels = ["min", "max"] if args.include_max else ["min"]

    kept, seen = [], set()
    stats = {}
    for level in levels:
        sub = [t for t, lv in rows if lv == level]
        got = 0
        for t in sub:
            line = survives(t)
            if line and line not in seen:
                seen.add(line)
                kept.append({"text": line, "level": level})
                got += 1
        stats[level] = (len(sub), got)
    for level, (total, got) in stats.items():
        print(f"{level}: {total} 条 -> 保留 {got} 条")

    data = {
        "source": "https://github.com/cndiandian/zuanbot.com",
        "license": "Apache-2.0",
        "license_file": "https://github.com/cndiandian/zuanbot.com/blob/master/LICENSE",
        "note": "只筛了 min 口吐芬芳。max 火力全开没打包: 大量性/杀全家内容, 是 Riot 26.5 要封号的严重辱骂, 不是过滤器问题。不要直接爬 caonima.de, 站长在首页写了未经许可不要抄袭, 同一份库在 GitHub 是 Apache-2.0 开源的, 走这里才是合法路线。",
        "levels": levels,
        "count": len(kept),
        "lines": kept,
    }
    OUT.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"写出 {OUT} ({len(kept)} 条, {OUT.stat().st_size} 字节)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
