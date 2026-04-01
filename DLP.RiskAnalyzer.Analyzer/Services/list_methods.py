import re

path = r"c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\DLP.RiskAnalyzer.Analyzer\Services\DatabaseService.cs"

with open(path, "r", encoding="utf-8") as f:
    text = f.read()

pattern = re.compile(r"^\s*(public|private|protected)\s+(async\s+)?(virtual\s+)?(Task\s*<[^\n]+>|Task|void|[\w<,>]+)\s+(\w+)\s*\(", re.MULTILINE)
out = ""
for m in pattern.finditer(text):
    line_num = text[:m.start()].count('\n') + 1
    out += f"Line {line_num}: {m.group(0).strip()}\n"

with open("methods.txt", "w", encoding="utf-8") as out_f:
    out_f.write(out)
print("done")
