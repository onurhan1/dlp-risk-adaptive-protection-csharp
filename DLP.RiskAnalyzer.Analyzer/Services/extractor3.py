import os

path = r"c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\DLP.RiskAnalyzer.Analyzer\Services\DatabaseService.cs"

with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Remember lines are 0-indexed in array.
# RedisStreamProcessor: 174-555 (index 173-554)
redis_part1 = "".join(lines[173:555])
# RedisStreamProcessor helpers: 695 to end (index 694 to -2 (excluding final `}`))
redis_part2 = "".join(lines[694:-2])
redis_code = redis_part1 + "\n" + redis_part2

# ReleasedIncidentProcessor: 556-694 (index 555-693)
released_code = "".join(lines[555:694])

# Write RedisStreamProcessor.cs
with open(os.path.join(os.path.dirname(path), 'RedisStreamProcessor.cs'), 'w', encoding='utf-8') as rf:
    rf.write("""using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IRedisStreamProcessor
{
    Task<int> ProcessRedisStreamAsync();
}

public class RedisStreamProcessor : IRedisStreamProcessor
{
    private readonly AnalyzerDbContext _context;
    private readonly IConnectionMultiplexer _redis;
    private readonly PolicyExceptionSyncService _policyExceptionSyncService;
    private readonly ILogger<RedisStreamProcessor> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RedisStreamProcessor(
        AnalyzerDbContext context,
        IConnectionMultiplexer redis,
        PolicyExceptionSyncService policyExceptionSyncService,
        ILogger<RedisStreamProcessor> logger)
    {
        _context = context;
        _redis = redis;
        _policyExceptionSyncService = policyExceptionSyncService;
        _logger = logger;
    }

""")
    rf.write(redis_code)
    rf.write("}\n")
    
# Write ReleasedIncidentProcessor.cs
with open(os.path.join(os.path.dirname(path), 'ReleasedIncidentProcessor.cs'), 'w', encoding='utf-8') as rlf:
    rlf.write("""using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IReleasedIncidentProcessor
{
    Task<int> ProcessReleasedIncidentsStreamAsync();
}

public class ReleasedIncidentProcessor : IReleasedIncidentProcessor
{
    private readonly AnalyzerDbContext _context;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ReleasedIncidentProcessor> _logger;

    public ReleasedIncidentProcessor(
        AnalyzerDbContext context,
        IConnectionMultiplexer redis,
        ILogger<ReleasedIncidentProcessor> logger)
    {
        _context = context;
        _redis = redis;
        _logger = logger;
    }

""")
    rlf.write(released_code)
    rlf.write("}\n")

# Remove from DatabaseService.cs
new_lines = lines[:173] + ["}\n"]

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print("Extraction successful via exact line numbers.")
