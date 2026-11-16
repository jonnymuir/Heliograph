"""Throwaway spike: report which markers can still be recovered from a .docx.

    python3 inspect.py <file.docx>...
"""
import re
import sys
import zipfile
from collections import Counter

PARTS = ["word/document.xml", "word/header1.xml", "word/footer1.xml"]


def parts(path):
    with zipfile.ZipFile(path) as z:
        names = z.namelist()
        for name in names:
            if re.fullmatch(r"word/(document|header\d*|footer\d*)\.xml", name):
                yield name, z.read(name).decode("utf-8")


def report(path):
    tags, split_tags, fields, simple, sdts, sdt_shapes = Counter(), 0, Counter(), 0, Counter(), Counter()
    for name, xml in parts(path):
        where = "body" if "document" in name else ("header" if "header" in name else "footer")
        for p in re.findall(r"<w:p[ >].*?</w:p>", xml, flags=re.S):
            runs = re.findall(r"<w:t[^>]*>([^<]*)</w:t>", p)
            text = "".join(runs)
            for tag in re.findall(r"\(\((.*?)\)\)", text):
                tags[where] += 1
                if not any(f"(({tag}))" in r for r in runs):
                    split_tags += 1
        for instr in re.findall(r"<w:instrText[^>]*>([^<]*)</w:instrText>", xml):
            m = re.search(r"MERGEFIELD\s+(\S+)", instr)
            if m:
                fields[(where, m.group(1).split(":")[0] if ":" in m.group(1) else "value")] += 1
        for instr in re.findall(r'<w:fldSimple[^>]*w:instr="([^"]*)"', xml):
            m = re.search(r"MERGEFIELD\s+(\S+)", instr)
            if m:
                simple += 1
                fields[(where, m.group(1).split(":")[0] if ":" in m.group(1) else "value")] += 1
        for tag in re.findall(r'<w:tag w:val="([^"]*)"', xml):
            sdts[(where, tag.split(":")[0])] += 1
        # What does each control wrap? The element that follows <w:sdtContent>.
        for tag, first in re.findall(r'<w:tag w:val="([^"]*)"/>.*?<w:sdtContent>\s*<(w:\w+)', xml, flags=re.S):
            sdt_shapes[(tag.split(":")[0], first)] += 1
        if "repeatingSection" in xml:
            sdt_shapes[("repeatingSection element", "kept")] += xml.count("repeatingSection")
    print(f"\n{path}")
    print("  text tags       :", dict(tags), "| split across runs:", split_tags)
    print("  merge fields    :", {f"{k[0]}/{k[1]}": v for k, v in sorted(fields.items())}, "| as fldSimple:", simple)
    print("  content controls:", {f"{k[0]}/{k[1]}": v for k, v in sorted(sdts.items())})
    print("  controls wrap   :", {f"{k[0]}->{k[1]}": v for k, v in sorted(sdt_shapes.items())})


for f in sys.argv[1:]:
    report(f)
