import os
import re

path = r"c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\DLP.RiskAnalyzer.Analyzer\Services\DatabaseService.cs"

with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

# I will just write regex to find the methods.
redis_pattern = re.compile(r"    public async Task<int> ProcessRedisStreamAsync.*?    }\n\n    private void ProcessIncidentValues.*?    }\n", re.DOTALL)
redis_match = redis_pattern.search(text)

released_pattern = re.compile(r"    public async Task<int> ProcessReleasedIncidentsStreamAsync.*?    public async Task<int> CheckAndUpdateReleasedIncidentsAsync\(CancellationToken cancellationToken = default\).*?    }\n", re.DOTALL)
released_match = released_pattern.search(text)

if redis_match and released_match:
    redis_code = redis_match.group(0)
    released_code = released_match.group(0)
    
    # Create RedisStreamProcessor
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
        rf.write("\n}\n")
        
    # Create ReleasedIncidentProcessor
    with open(os.path.join(os.path.dirname(path), 'ReleasedIncidentProcessor.cs'), 'w', encoding='utf-8') as rlf:
        rlf.write("""using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IReleasedIncidentProcessor
{
    Task<int> ProcessReleasedIncidentsStreamAsync();
    Task<int> CheckAndUpdateReleasedIncidentsAsync(CancellationToken cancellationToken = default);
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
        rlf.write("\n}\n")

    # Remove from DatabaseService
    new_text = text.replace(redis_code, "")
    new_text = new_text.replace(released_code, "")
    
    with open(path, 'w', encoding='utf-8') as f:
        f.write(new_text)
    
    print("Extraction successful.")
else:
    print("Could not find patterns.")
