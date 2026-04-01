import os

d = r"c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\DLP.RiskAnalyzer.Analyzer\Services"

# 1. Update IBehaviorEngineService.cs
p = os.path.join(d, "IBehaviorEngineService.cs")
with open(p, 'r', encoding='utf-8') as f: text = f.read()
text = text.replace(
    "AnalyzeEntityAsync(\n        string entityType,\n        string entityId,\n        int lookbackDays = 30,\n        bool forceRefresh = false);",
    "AnalyzeEntityAsync(\n        string entityType,\n        string entityId,\n        int lookbackDays = 7);"
)
with open(p, 'w', encoding='utf-8') as f: f.write(text)

# 2. Update IAnomalyDetector.cs
p = os.path.join(d, "IAnomalyDetector.cs")
with open(p, 'r', encoding='utf-8') as f: text = f.read()
text = text.replace(
    "Task<Dictionary<string, double>> CalculateUserBaselineAsync(\n        string userEmail, int lookbackDays = 30);",
    "Task<Dictionary<string, double>> CalculateUserBaselineAsync(\n        string userEmail, string metricType);"
)
text = text.replace(
    "Task<Dictionary<string, object>> DetectAnomaliesAsync(\n        string userEmail, int lookbackDays = 30);",
    "Task<Dictionary<string, object>> DetectAnomaliesAsync(\n        string userEmail, double currentValue, string metricType = \"cloud_upload\");"
)
with open(p, 'w', encoding='utf-8') as f: f.write(text)

# 3. Update IDatabaseService.cs
p = os.path.join(d, "IDatabaseService.cs")
with open(p, 'r', encoding='utf-8') as f: text = f.read()
text = text.replace("    Task<int> ProcessRedisStreamAsync();\n\n    Task<int> ProcessReleasedIncidentsStreamAsync();\n", "")
with open(p, 'w', encoding='utf-8') as f: f.write(text)

# 4. Move GetEffectiveMaxMatches
p1 = os.path.join(d, "BehaviorEngineService.cs")
with open(p1, 'r', encoding='utf-8') as f: be_lines = f.readlines()

eff_method_lines = []
for i, line in enumerate(be_lines):
    if "private int _metricsCalculator.GetEffectiveMaxMatches" in line or "/// <summary>\n" in line and "Get effective MaxMatches" in be_lines[i+1]:
        # found it
        start = i
        if "///" not in line:
            start = i - 3
        # find end
        end = start
        braces = 0
        in_method = False
        for j in range(start, len(be_lines)):
            if "{" in be_lines[j]: 
                braces += be_lines[j].count("{")
                in_method = True
            if "}" in be_lines[j]: 
                braces -= be_lines[j].count("}")
            if in_method and braces == 0:
                end = j
                break
        eff_method_lines = be_lines[start:end+1]
        
        # fix the signature in the copied lines
        for k in range(len(eff_method_lines)):
            if "private int _metricsCalculator.GetEffectiveMaxMatches" in eff_method_lines[k]:
                eff_method_lines[k] = eff_method_lines[k].replace("private int _metricsCalculator.", "public int ")
        
        # remove from BehaviorEngineService
        del be_lines[start:end+1]
        break

with open(p1, 'w', encoding='utf-8') as f: f.writelines(be_lines)

if eff_method_lines:
    p2 = os.path.join(d, "BehaviorMetricsCalculator.cs")
    with open(p2, 'r', encoding='utf-8') as f: calc_lines = f.readlines()
    # insert before the last brace
    calc_lines = calc_lines[:-2] + ["\n"] + eff_method_lines + ["\n}\n"]
    with open(p2, 'w', encoding='utf-8') as f: f.writelines(calc_lines)

print("Fixes applied.")
