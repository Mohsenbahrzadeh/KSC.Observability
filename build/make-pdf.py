#!/usr/bin/env python
"""
Render a Markdown guide to a print-ready HTML file (RTL, Persian-friendly) and then
to PDF via headless Chrome/Edge.

Usage:
    python build/make-pdf.py docs/GUIDE.fa.md docs/KSC.Observability-Guide.fa.pdf
"""
import sys, os, re, subprocess, glob

import markdown


def slugify(value, separator='-'):
    """Unicode-friendly slug so Persian headings get stable ids."""
    value = value.strip().lower()
    for ch in ['.', '؟', '?', ':', '،', ',', '(', ')', '`']:
        value = value.replace(ch, '')
    value = re.sub(r'\s+', separator, value)
    return value


HTML_TEMPLATE = """<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
<meta charset="utf-8">
<title>{title}</title>
<style>
@page {{ size: A4; margin: 18mm 15mm; }}
* {{ box-sizing: border-box; }}
body {{
  font-family: 'Segoe UI', 'Tahoma', 'Vazirmatn', 'Iranian Sans', sans-serif;
  direction: rtl; text-align: right;
  font-size: 11pt; line-height: 1.75; color: #1f2328;
  margin: 0; padding: 0;
}}
h1, h2, h3, h4 {{ line-height: 1.35; page-break-after: avoid; font-weight: 700; }}
h1 {{ font-size: 23pt; color: #0b3d91; border-bottom: 3px solid #0b3d91; padding-bottom: .3rem; }}
h2 {{ font-size: 16pt; color: #0b3d91; border-bottom: 1px solid #d0d7de; padding-bottom: .25rem; margin-top: 1.6rem; }}
h3 {{ font-size: 13pt; color: #1a4a8a; margin-top: 1.2rem; }}
h4 {{ font-size: 11.5pt; color: #333; }}
p, li {{ orphans: 2; widows: 2; }}
a {{ color: #2563eb; text-decoration: none; }}
blockquote {{
  border-right: 4px solid #0b3d91; background: #f3f6fb;
  margin: .8rem 0; padding: .5rem 1rem; color: #333;
}}
ul, ol {{ padding-right: 1.4rem; }}
hr {{ border: none; border-top: 1px solid #d0d7de; margin: 1.5rem 0; }}
table {{ border-collapse: collapse; width: 100%; margin: 1rem 0; font-size: 10pt; page-break-inside: avoid; }}
th, td {{ border: 1px solid #c9d1d9; padding: 6px 9px; text-align: right; vertical-align: top; }}
th {{ background: #eef3fb; font-weight: 700; }}
tr:nth-child(even) td {{ background: #fafbfc; }}
/* code is always left-to-right */
code {{
  direction: ltr; unicode-bidi: embed;
  font-family: Consolas, 'Courier New', monospace; font-size: 9.5pt;
  background: #f1f3f5; padding: .08rem .3rem; border-radius: 3px;
}}
pre {{
  direction: ltr; text-align: left;
  background: #f6f8fa; border: 1px solid #e1e4e8; border-radius: 6px;
  padding: .7rem .9rem; overflow: visible; white-space: pre-wrap; word-wrap: break-word;
  page-break-inside: avoid; font-size: 9pt; line-height: 1.5;
}}
pre code {{ background: none; padding: 0; font-size: 9pt; }}
.cover {{ text-align: center; padding: 2rem 0 1rem; }}
</style>
</head>
<body>
{body}
</body>
</html>
"""


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else 'docs/GUIDE.fa.md'
    out_pdf = sys.argv[2] if len(sys.argv) > 2 else 'docs/KSC.Observability-Guide.fa.pdf'
    out_html = os.path.splitext(out_pdf)[0] + '.html'

    text = open(src, encoding='utf-8').read()
    # Drop the outer <div dir="rtl"> wrapper so Markdown processes the inner content.
    text = re.sub(r'^\s*<div dir="rtl">\s*', '', text)
    text = re.sub(r'\s*</div>\s*$', '', text)

    md = markdown.Markdown(extensions=['tables', 'fenced_code', 'sane_lists', 'toc', 'attr_list'],
                           extension_configs={'toc': {'slugify': slugify}})
    body = md.convert(text)
    html = HTML_TEMPLATE.format(title='KSC.Observability — Guide', body=body)
    open(out_html, 'w', encoding='utf-8').write(html)
    print('HTML written:', out_html)

    # Find a Chromium-based browser.
    candidates = [
        r'C:\Program Files\Google\Chrome\Application\chrome.exe',
        r'C:\Program Files (x86)\Google\Chrome\Application\chrome.exe',
        r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
        r'C:\Program Files\Microsoft\Edge\Application\msedge.exe',
    ]
    browser = next((c for c in candidates if os.path.exists(c)), None)
    if not browser:
        print('No Chromium browser found; HTML produced, skipping PDF.')
        return

    abs_html = os.path.abspath(out_html).replace('\\', '/')
    abs_pdf = os.path.abspath(out_pdf)
    cmd = [browser, '--headless', '--disable-gpu', '--no-pdf-header-footer',
           '--print-to-pdf=' + abs_pdf, 'file:///' + abs_html]
    print('Rendering PDF with:', os.path.basename(browser))
    subprocess.run(cmd, timeout=120,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if os.path.exists(out_pdf) and os.path.getsize(out_pdf) > 0:
        print('PDF written: %s (%.0f KB)' % (out_pdf, os.path.getsize(out_pdf) / 1024))
    else:
        print('PDF generation failed.')
        sys.exit(1)


if __name__ == '__main__':
    main()
