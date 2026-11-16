"""Throwaway spike: build a cut-down annual statement three ways (text tags, merge fields,
content controls) so each can be put through an editor's save and inspected afterwards.

    python3 build.py <out-dir>
"""
import sys
import zipfile
from pathlib import Path

W = 'xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"'
R = 'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"'
W15 = 'xmlns:w15="http://schemas.microsoft.com/office/word/2012/wordml"'

CONTENT_TYPES = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
<Default Extension="xml" ContentType="application/xml"/>
<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
<Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
<Override PartName="/word/header1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml"/>
<Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>
</Types>"""

RELS = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>"""

DOC_RELS = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/header" Target="header1.xml"/>
<Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/>
</Relationships>"""

STYLES = f"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles {W}>
<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="24"/><w:lang w:val="en-GB"/></w:rPr></w:rPrDefault>
<w:pPrDefault><w:pPr><w:spacing w:after="160"/></w:pPr></w:pPrDefault></w:docDefaults>
<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
<w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:pPr><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:b/><w:sz w:val="36"/></w:rPr></w:style>
<w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:pPr><w:outlineLvl w:val="1"/></w:pPr><w:rPr><w:b/><w:sz w:val="28"/></w:rPr></w:style>
<w:style w:type="table" w:styleId="TableGrid"><w:name w:val="Table Grid"/><w:tblPr><w:tblBorders>
<w:top w:val="single" w:sz="4"/><w:left w:val="single" w:sz="4"/><w:bottom w:val="single" w:sz="4"/><w:right w:val="single" w:sz="4"/><w:insideH w:val="single" w:sz="4"/><w:insideV w:val="single" w:sz="4"/>
</w:tblBorders></w:tblPr></w:style>
</w:styles>"""


def run(text, bold=False):
    props = "<w:rPr><w:b/></w:rPr>" if bold else ""
    return f'<w:r>{props}<w:t xml:space="preserve">{text}</w:t></w:r>'


def para(inner, style=None):
    props = f'<w:pPr><w:pStyle w:val="{style}"/></w:pPr>' if style else ""
    return f"<w:p>{props}{inner}</w:p>"


def cell(inner, right=False):
    props = '<w:pPr><w:jc w:val="right"/></w:pPr>' if right else ""
    return f"<w:tc><w:tcPr><w:tcW w:w=\"2300\" w:type=\"dxa\"/></w:tcPr><w:p>{props}{inner}</w:p></w:tc>"


def row(cells, header=False):
    props = "<w:trPr><w:cantSplit/>" + ("<w:tblHeader/>" if header else "") + "</w:trPr>"
    return f"<w:tr>{props}{''.join(cells)}</w:tr>"


def table(rows):
    return ('<w:tbl><w:tblPr><w:tblStyle w:val="TableGrid"/><w:tblW w:w="0" w:type="auto"/></w:tblPr>'
            '<w:tblGrid><w:gridCol w:w="2300"/><w:gridCol w:w="2300"/><w:gridCol w:w="2300"/><w:gridCol w:w="2300"/></w:tblGrid>'
            + "".join(rows) + "</w:tbl>")


# --- the three ways of marking -------------------------------------------------------------

class Tags:
    """Plain text an author can type or paste: ((Member name))."""
    name = "tags"

    def value(self, label, key):
        return run(f"(({label}))")

    def split_value(self, label, key):
        # What Word does to typed text after edits: one tag spread over several runs.
        half = len(label) // 2
        return run("((" + label[:half]) + run(label[half:], bold=True) + run("))")

    def condition(self, label, key, blocks):
        return para(run(f"((if {label}))")) + blocks + para(run("((end if))"))

    def repeat_row(self, label, key, cells):
        cells = list(cells)
        return row([cell(run(f"((each {label}))") + cells[0][1], cells[0][2])]
                   + [cell(c[1], c[2]) for c in cells[1:-1]]
                   + [cell(cells[-1][1] + run("((end each))"), cells[-1][2])])


class MergeFields:
    """The word processor's own mail-merge field, with regions marked by a naming convention."""
    name = "mergefields"

    def _field(self, instr, shown):
        return ('<w:r><w:fldChar w:fldCharType="begin"/></w:r>'
                f'<w:r><w:instrText xml:space="preserve"> MERGEFIELD {instr} </w:instrText></w:r>'
                '<w:r><w:fldChar w:fldCharType="separate"/></w:r>'
                f'<w:r><w:t>«{shown}»</w:t></w:r>'
                '<w:r><w:fldChar w:fldCharType="end"/></w:r>')

    def value(self, label, key):
        return self._field(key, label)

    split_value = value

    def condition(self, label, key, blocks):
        return (para(self._field(f"If:{key}", f"If {label}")) + blocks
                + para(self._field(f"EndIf:{key}", f"End if {label}")))

    def repeat_row(self, label, key, cells):
        cells = list(cells)
        return row([cell(self._field(f"TableStart:{key}", f"Each {label}") + cells[0][1], cells[0][2])]
                   + [cell(c[1], c[2]) for c in cells[1:-1]]
                   + [cell(cells[-1][1] + self._field(f"TableEnd:{key}", "End each"), cells[-1][2])])


class ContentControls:
    """Structured document tags: the control wraps the content it governs."""
    name = "contentcontrols"

    def _sdt(self, tag, alias, content, extra=""):
        return (f'<w:sdt><w:sdtPr><w:alias w:val="{alias}"/><w:tag w:val="{tag}"/>{extra}</w:sdtPr>'
                f"<w:sdtContent>{content}</w:sdtContent></w:sdt>")

    def value(self, label, key):
        return self._sdt(f"value:{key}", label, run(f"[{label}]"))

    split_value = value

    def condition(self, label, key, blocks):
        return self._sdt(f"if:{key}", f"Only when {label}", blocks)

    def repeat_row(self, label, key, cells):
        inner = row([cell(c[1], c[2]) for c in cells])
        item = self._sdt(f"item:{key}", f"{label} row", inner, "<w15:repeatingSectionItem/>")
        return self._sdt(f"each:{key}", f"For each {label}", item, "<w15:repeatingSection/>")


def build(m):
    v = m.value
    body = "".join([
        para(v("Member name", "memberName")),
        para(v("Member address", "memberAddress")),
        para(v("Letter date", "letterDate")),
        para(run("Member reference: ") + v("Member reference", "memberReference")),
        para(run("Dear ") + m.split_value("Member name", "memberName") + run(",")),
        para(run("Your contributions statement"), "Heading1"),
        para(run("This statement covers ") + v("Statement start", "statementStart") + run(" to ")
             + v("Statement end", "statementEnd") + run(". You are a ") + v("Membership tier", "membershipTier")
             + run(" member.")),
        m.condition("Has contributions", "hasContributions", table([
            row([cell(run("Date", True)), cell(run("Description", True)), cell(run("Amount", True), True),
                 cell(run("Running total", True), True)], header=True),
            m.repeat_row("Contributions", "contributions", [
                ("date", v("Date", "date"), False),
                ("description", v("Description", "description"), False),
                ("amount", v("Amount", "amount"), True),
                ("runningTotal", v("Running total", "runningTotal"), True),
            ]),
            row([cell(""), cell(run("Total paid", True)), cell(""), cell(v("Total paid", "totalPaid"), True)]),
        ])),
        m.condition("No contributions", "noContributions",
                    para(run("We did not receive any contributions from you in this period."))),
        m.condition("Owes money", "owesMoney",
                    para(run("You owe ") + v("Amount owed", "amountOwed"), "Heading2")
                    + para(run("Pay ") + v("Amount owed", "amountOwed") + run(" by ")
                           + v("Payment due date", "paymentDueDate") + run(" to keep your membership."))),
        para(run("Yours sincerely,")),
        para(run("Contributions Team")),
    ])
    sect = ('<w:sectPr><w:headerReference w:type="default" r:id="rId2"/><w:footerReference w:type="default" r:id="rId3"/>'
            '<w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="708" w:footer="708"/></w:sectPr>')
    document = f'<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document {W} {R} {W15}><w:body>{body}{sect}</w:body></w:document>'
    header = (f'<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:hdr {W} {R} {W15}>'
              + para(run("National Juggling Federation: ") + v("Member name", "memberName") + run(", ")
                     + v("Member reference", "memberReference")) + "</w:hdr>")
    footer = (f'<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:ftr {W} {R} {W15}>'
              + para(run("Office: ") + v("Regional office", "regionalOffice") + run("   Page ")
                     + '<w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText> PAGE </w:instrText></w:r>'
                       '<w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>1</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r>')
              + "</w:ftr>")
    return document, header, footer


def write(path, m):
    document, header, footer = build(m)
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("[Content_Types].xml", CONTENT_TYPES)
        z.writestr("_rels/.rels", RELS)
        z.writestr("word/_rels/document.xml.rels", DOC_RELS)
        z.writestr("word/styles.xml", STYLES)
        z.writestr("word/document.xml", document)
        z.writestr("word/header1.xml", header)
        z.writestr("word/footer1.xml", footer)


if __name__ == "__main__":
    out = Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    for marking in (Tags(), MergeFields(), ContentControls()):
        write(out / f"statement-{marking.name}.docx", marking)
        print("built", out / f"statement-{marking.name}.docx")
