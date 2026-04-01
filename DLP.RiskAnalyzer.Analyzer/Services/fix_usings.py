import os

base_dir = r"c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\DLP.RiskAnalyzer.Analyzer"

def add_using(relative_path, using_stmt):
    path = os.path.join(base_dir, relative_path)
    if not os.path.exists(path): return
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    if using_stmt not in content:
        # Find first using and insert before it, or at top
        if "using " in content:
            content = content.replace("using ", f"{using_stmt}\nusing ", 1)
        else:
            content = f"{using_stmt}\n\n{content}"
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"Added to {relative_path}")

add_using(r"Repositories\Implementations\AIAnalysisRepository.cs", "using DLP.RiskAnalyzer.Analyzer.Models;")
add_using(r"Repositories\Interfaces\IAIAnalysisRepository.cs", "using DLP.RiskAnalyzer.Analyzer.Models;")
add_using(r"Services\IBehaviorEngineService.cs", "using DLP.RiskAnalyzer.Analyzer.Models;")
add_using(r"Services\DatabaseService.cs", "using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;")
add_using(r"Services\IUserInsightsService.cs", "using DLP.RiskAnalyzer.Analyzer.Models;")
add_using(r"Services\BehaviorEngineService.cs", "using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;")
add_using(r"Services\BehaviorEngineService.cs", "using DLP.RiskAnalyzer.Analyzer.Models;")

print("Done usings")
