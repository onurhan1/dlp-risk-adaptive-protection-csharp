using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace DLP.RiskAnalyzer.Tests.Services
{
    public class RedisStreamProcessorTests
    {
        private readonly Mock<IConnectionMultiplexer> _redisMock;
        private readonly Mock<IDatabase> _dbMock;
        private readonly Mock<IPolicyExceptionSyncService> _policySyncMock;
        private readonly Mock<ILogger<RedisStreamProcessor>> _loggerMock;
        private readonly Mock<IIncidentRepository> _incidentRepoMock;
        private readonly RedisStreamProcessor _processor;

        public RedisStreamProcessorTests()
        {
            _redisMock = new Mock<IConnectionMultiplexer>();
            _dbMock = new Mock<IDatabase>();
            _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);

            _policySyncMock = new Mock<IPolicyExceptionSyncService>();
            _loggerMock = new Mock<ILogger<RedisStreamProcessor>>();
            _incidentRepoMock = new Mock<IIncidentRepository>();

            // Mock DB context is not actually used in ProcessRedisStreamAsync, but we provide null directly 
            // since the constructor assigns it to a readonly field safely.
            _processor = new RedisStreamProcessor(
                null!,
                _redisMock.Object,
                _policySyncMock.Object,
                _loggerMock.Object,
                _incidentRepoMock.Object
            );
        }

        [Fact]
        public async Task ProcessRedisStreamAsync_WhenNoMessages_ReturnsZero()
        {
            // Arrange
            _policySyncMock.Setup(p => p.GetExceptionLookupAsync()).ReturnsAsync(new Dictionary<string, string>());
            
            // Empty streams for both Pending and New phases
            _dbMock.Setup(d => d.StreamReadGroupAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<CommandFlags>())
            ).ReturnsAsync(Array.Empty<StreamEntry>());

            // Act
            var itemsProcessed = await _processor.ProcessRedisStreamAsync();

            // Assert
            Assert.Equal(0, itemsProcessed);
            
            _dbMock.Verify(d => d.StreamCreateConsumerGroupAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue?>(), It.IsAny<bool>(), It.IsAny<CommandFlags>()), Times.Once); // Runs during initialization
        }

        [Fact]
        public async Task ProcessRedisStreamAsync_WithValidIncident_SavesIncident()
        {
            // Arrange
            _policySyncMock.Setup(p => p.GetExceptionLookupAsync()).ReturnsAsync(new Dictionary<string, string>());

            // Create a fake Redis Stream Message
            var entries = new[]
            {
                new NameValueEntry("id", "12345"),
                new NameValueEntry("user", "test@test.com"),
                new NameValueEntry("timestamp", DateTime.UtcNow.ToString("O")),
                new NameValueEntry("severity", "3")
            };
            var streamMessage = new StreamEntry("123456789-0", entries);

            // Setup the mock to return message for the very first call, then empty for all subsequent calls
            _dbMock.SetupSequence(d => d.StreamReadGroupAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<CommandFlags>())
            ).ReturnsAsync(new[] { streamMessage })
             .ReturnsAsync(Array.Empty<StreamEntry>())
             .ReturnsAsync(Array.Empty<StreamEntry>())
             .ReturnsAsync(Array.Empty<StreamEntry>());

            _incidentRepoMock.Setup(r => r.GetByIdAsync(12345)).ReturnsAsync((Incident?)null); // Ensure it's treated as new

            _incidentRepoMock.Setup(r => r.BulkInsertIncidentsAsync(It.IsAny<IEnumerable<Incident>>()))
                .ReturnsAsync((1, 0)); // 1 saved, 0 skipped

            // Act
            var itemsProcessed = await _processor.ProcessRedisStreamAsync();

            // Assert
            Assert.Equal(1, itemsProcessed);
            
            _incidentRepoMock.Verify(r => r.BulkInsertIncidentsAsync(
                It.Is<IEnumerable<Incident>>(list => ((List<Incident>)list).Count == 1 && ((List<Incident>)list)[0].Id == 12345 && ((List<Incident>)list)[0].UserEmail == "test@test.com")
            ), Times.Once);
            
            _dbMock.Verify(d => d.StreamAcknowledgeAsync("dlp:incidents", "analyzer", streamMessage.Id, CommandFlags.None), Times.Once);
        }
    }
}
