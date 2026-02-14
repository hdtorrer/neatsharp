using FluentAssertions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Genetics;

public class InnovationTrackerTests
{
    [Fact]
    public void GetConnectionInnovation_NovelConnection_AssignsNewId()
    {
        var tracker = new InnovationTracker();

        int id = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);

        id.Should().Be(0);
    }

    [Fact]
    public void GetConnectionInnovation_SameConnectionSameGeneration_ReturnsSameId()
    {
        var tracker = new InnovationTracker();

        int id1 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);
        int id2 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);

        id1.Should().Be(id2);
    }

    [Fact]
    public void GetConnectionInnovation_DifferentConnections_GetDifferentIds()
    {
        var tracker = new InnovationTracker();

        int id1 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);
        int id2 = tracker.GetConnectionInnovation(sourceNodeId: 1, targetNodeId: 5);

        id2.Should().NotBe(id1);
    }

    [Fact]
    public void GetConnectionInnovation_IdsAreMonotonicallyIncreasing()
    {
        var tracker = new InnovationTracker();

        int id1 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);
        int id2 = tracker.GetConnectionInnovation(sourceNodeId: 1, targetNodeId: 5);
        int id3 = tracker.GetConnectionInnovation(sourceNodeId: 2, targetNodeId: 5);

        id2.Should().BeGreaterThan(id1);
        id3.Should().BeGreaterThan(id2);
    }

    [Fact]
    public void GetNodeSplitInnovation_AssignsNewNodeIdAndTwoConnectionInnovations()
    {
        var tracker = new InnovationTracker();

        NodeSplitResult result = tracker.GetNodeSplitInnovation(connectionInnovation: 1);

        result.NewNodeId.Should().Be(0);
        result.IncomingConnectionInnovation.Should().Be(0);
        result.OutgoingConnectionInnovation.Should().Be(1);
    }

    [Fact]
    public void GetNodeSplitInnovation_SameSplitSameGeneration_ReturnsIdenticalResult()
    {
        var tracker = new InnovationTracker();

        NodeSplitResult result1 = tracker.GetNodeSplitInnovation(connectionInnovation: 1);
        NodeSplitResult result2 = tracker.GetNodeSplitInnovation(connectionInnovation: 1);

        result1.Should().Be(result2);
    }

    [Fact]
    public void NextGeneration_ClearsDedupCaches()
    {
        var tracker = new InnovationTracker();

        int id1 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);
        tracker.NextGeneration();
        int id2 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);

        id2.Should().NotBe(id1);
        id2.Should().BeGreaterThan(id1);
    }

    [Fact]
    public void NextGeneration_PreservesCounters()
    {
        var tracker = new InnovationTracker();

        // Assign innovation ID 0
        tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);
        tracker.NextGeneration();
        // After clearing cache, next novel connection should get ID 1 (not 0)
        int id = tracker.GetConnectionInnovation(sourceNodeId: 1, targetNodeId: 6);

        id.Should().Be(1);
    }

    [Fact]
    public void NextGeneration_ClearsNodeSplitCache()
    {
        var tracker = new InnovationTracker();

        NodeSplitResult result1 = tracker.GetNodeSplitInnovation(connectionInnovation: 1);
        tracker.NextGeneration();
        NodeSplitResult result2 = tracker.GetNodeSplitInnovation(connectionInnovation: 1);

        result2.Should().NotBe(result1);
        result2.NewNodeId.Should().BeGreaterThan(result1.NewNodeId);
    }

    [Fact]
    public void Constructor_WithCustomStartValues_InitializesCounters()
    {
        var tracker = new InnovationTracker(startInnovationNumber: 100, startNodeId: 50);

        int connectionId = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 1);
        NodeSplitResult splitResult = tracker.GetNodeSplitInnovation(connectionInnovation: 99);

        connectionId.Should().Be(100);
        splitResult.NewNodeId.Should().Be(50);
        splitResult.IncomingConnectionInnovation.Should().Be(101);
        splitResult.OutgoingConnectionInnovation.Should().Be(102);
    }

    [Fact]
    public void Constructor_Parameterless_StartsAtZero()
    {
        var tracker = new InnovationTracker();

        int connectionId = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 1);

        connectionId.Should().Be(0);
    }

    [Fact]
    public void GetConnectionInnovation_InterleavedWithNodeSplit_MaintainsSeparateCounters()
    {
        var tracker = new InnovationTracker();

        // Connection innovation gets ID 0
        int connId1 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);
        connId1.Should().Be(0);

        // Node split gets node ID 0, and connection innovations 1 and 2
        NodeSplitResult split = tracker.GetNodeSplitInnovation(connectionInnovation: 99);
        split.NewNodeId.Should().Be(0);
        split.IncomingConnectionInnovation.Should().Be(1);
        split.OutgoingConnectionInnovation.Should().Be(2);

        // Next connection innovation should be 3
        int connId2 = tracker.GetConnectionInnovation(sourceNodeId: 2, targetNodeId: 5);
        connId2.Should().Be(3);
    }

    [Fact]
    public void GetNodeSplitInnovation_DifferentConnections_GetDifferentResults()
    {
        var tracker = new InnovationTracker();

        NodeSplitResult result1 = tracker.GetNodeSplitInnovation(connectionInnovation: 1);
        NodeSplitResult result2 = tracker.GetNodeSplitInnovation(connectionInnovation: 2);

        result2.NewNodeId.Should().BeGreaterThan(result1.NewNodeId);
        result2.IncomingConnectionInnovation.Should().BeGreaterThan(result1.OutgoingConnectionInnovation);
    }

    [Fact]
    public void MultipleGenerations_CountersNeverReset()
    {
        var tracker = new InnovationTracker();

        // Generation 1
        tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 1); // ID 0
        tracker.GetConnectionInnovation(sourceNodeId: 1, targetNodeId: 2); // ID 1
        tracker.NextGeneration();

        // Generation 2
        tracker.GetConnectionInnovation(sourceNodeId: 2, targetNodeId: 3); // ID 2
        tracker.NextGeneration();

        // Generation 3
        int id = tracker.GetConnectionInnovation(sourceNodeId: 3, targetNodeId: 4); // ID 3
        id.Should().Be(3);
    }
}
