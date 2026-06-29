import re
import os

path = r"c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\DLP.RiskAnalyzer.Collector\Services\CollectorBackgroundService.cs"

with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

# Make sure to import Mappers
if "using DLP.RiskAnalyzer.Collector.Mappers;" not in text:
    text = text.replace("using DLP.RiskAnalyzer.Collector.Services;", "using DLP.RiskAnalyzer.Collector.Services;\nusing DLP.RiskAnalyzer.Collector.Mappers;")

manual_pattern = re.compile(r"                    var maxMatches = 0;.*?new System\.Text\.Json\.JsonSerializerOptions \{ DefaultIgnoreCondition = System\.Text\.Json\.Serialization\.JsonIgnoreCondition\.WhenWritingNull \}\)\n                            : null\n                    \};\n", re.DOTALL)
regular_pattern = re.compile(r"                    var maxMatches = 0;.*?new System\.Text\.Json\.JsonSerializerOptions \{ DefaultIgnoreCondition = System\.Text\.Json\.Serialization\.JsonIgnoreCondition\.WhenWritingNull \}\)\n                            : null\n                        \};\n", re.DOTALL)

def replacer(match):
    indent = match.group(0).split('var maxMatches = 0;')[0]
    return indent + "var incident = IncidentMapper.MapFromDLPIncident(dlpIncident);\n"

new_text = manual_pattern.sub(replacer, text)
new_text = regular_pattern.sub(replacer, new_text)

with open(path, 'w', encoding='utf-8') as f:
    f.write(new_text)

print("Replacement successful.")
