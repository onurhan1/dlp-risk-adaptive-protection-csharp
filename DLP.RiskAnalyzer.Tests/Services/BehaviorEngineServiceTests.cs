using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DLP.RiskAnalyzer.Tests.Services
{
    public class BehaviorEngineServiceTests
    {
        private readonly Mock<IIncidentRepository> _incidentRepositoryMock;
        private readonly Mock<IAIAnalysisRepository> _aiAnalysisRepositoryMock;
        private readonly Mock<ILogger<BehaviorEngineService>> _loggerMock;
        private readonly Mock<IBehaviorMetricsCalculator> _metricsCalculatorMock;
        private readonly Mock<IBehaviorAIExplanationService> _aiExplanationServiceMock;
        private readonly BehaviorEngineService _service;

        public BehaviorEngineServiceTests()
        {
            _incidentRepositoryMock = new Mock<IIncidentRepository>();
            _aiAnalysisRepositoryMock = new Mock<IAIAnalysisRepository>();
            _loggerMock = new Mock<ILogger<BehaviorEngineService>>();
            _metricsCalculatorMock = new Mock<IBehaviorMetricsCalculator>();
            _aiExplanationServiceMock = new Mock<IBehaviorAIExplanationService>();

            _service = new BehaviorEngineService(
                _incidentRepositoryMock.Object,
                _aiAnalysisRepositoryMock.Object,
                _loggerMock.Object,
                _metricsCalculatorMock.Object,
                _aiExplanationServiceMock.Object
            );
        }

        [Fact]
        public async Task AnalyzeEntityAsync_WithNoIncidents_ReturnsZeroRiskScore()
        {
            // Arrange
            string entityType = "user";
            string entityId = "test@example.com";
            
            _incidentRepositoryMock
                .Setup(repo => repo.GetIncidentsForEntityAsync(entityType, entityId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Incident>());

            // Act
            var result = await _service.AnalyzeEntityAsync(entityType, entityId, 7);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(entityType, result.EntityType);
            Assert.Equal(entityId, result.EntityId);
            Assert.Equal(0, result.RiskScore);
            Assert.Equal("low", result.AnomalyLevel);
            Assert.Contains("No incidents found", result.AIExplanation);
        }

        [Fact]
        public async Task AnalyzeEntityAsync_WithIncidents_CalculatesRiskProperly()
        {
            // Arrange
            string entityType = "user";
            string entityId = "test@example.com";
            
            var incidents = new List<Incident>
            {
                new Incident { Id = 1, UserEmail = entityId, RiskScore = 80, Severity = 3 }
            };

            // First call corresponds to current incidents (we pretend baseline is sufficient on second iteration)
            _incidentRepositoryMock
                .SetupSequence(repo => repo.GetIncidentsForEntityAsync(entityType, entityId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(incidents)  // current
                .ReturnsAsync(incidents); // baseline fallback

            var metricsCurrent = new BehaviorMetrics { TotalIncidents = 1, AvgSeverity = 3.0 };
            var metricsBaseline = new BehaviorMetrics { TotalIncidents = 1, AvgSeverity = 3.0 };
            
            _metricsCalculatorMock
                .Setup(m => m.CalculateEnhancedMetrics(It.IsAny<List<Incident>>()))
                .Returns(metricsCurrent);
                
            _metricsCalculatorMock
                .Setup(m => m.CalculateAllZScores(It.IsAny<BehaviorMetrics>(), It.IsAny<BehaviorMetrics>(), It.IsAny<List<Incident>>(), It.IsAny<List<Incident>>()))
                .Returns(new Dictionary<string, double>());

            _metricsCalculatorMock
                .Setup(m => m.CalculateEnhancedRiskScore(It.IsAny<Dictionary<string, double>>()))
                .Returns(65);

            _metricsCalculatorMock
                .Setup(m => m.CalculateThreatProfileMultiplier(It.IsAny<List<Incident>>()))
                .Returns(1.0);

            _metricsCalculatorMock
                .Setup(m => m.DetermineAnomalyLevel(65))
                .Returns("medium");

            _aiExplanationServiceMock
                .Setup(s => s.GenerateAIAnalysisAsync(entityType, entityId, It.IsAny<Dictionary<string, object>>(), It.IsAny<AnomalyResults>()))
                .ReturnsAsync(("AI Explanation", "AI Recommendation"));

            // Act
            var result = await _service.AnalyzeEntityAsync(entityType, entityId, 7);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(entityType, result.EntityType);
            Assert.Equal(entityId, result.EntityId);
            Assert.Equal(65, result.RiskScore);
            Assert.Equal("medium", result.AnomalyLevel);
            Assert.Equal("AI Explanation", result.AIExplanation);
            Assert.Equal("AI Recommendation", result.AIRecommendation);
        }
        
        [Fact]
        public async Task SaveAnalysisAsync_CallsRepository()
        {
            // Arrange
            var response = new AIBehavioralAnalysisResponse
            {
                EntityType = "user",
                EntityId = "test@test.com",
                AnalysisDate = DateTime.UtcNow,
                RiskScore = 90,
                AnomalyLevel = "high",
                AIExplanation = "Expl",
                AIRecommendation = "Rec"
            };

            // Act
            await _service.SaveAnalysisAsync(response);

            // Assert
            _aiAnalysisRepositoryMock.Verify(repo => repo.SaveAnalysisAsync(It.Is<AIBehavioralAnalysis>(a => 
                a.EntityType == response.EntityType &&
                a.EntityId == response.EntityId &&
                a.RiskScore == response.RiskScore
            )), Times.Once);
        }
    }
}
