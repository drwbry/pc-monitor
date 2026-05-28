using FluentAssertions;
using PcMonitor.Core.Models;
using PcMonitor.Core.Sensors;
using Xunit;

namespace PcMonitor.Core.Tests.Sensors;

public class ProcessSamplerTests
{
    private sealed class StubEnumerator : IProcessEnumerator
    {
        public IReadOnlyList<RawProcess> Next { get; set; } = Array.Empty<RawProcess>();
        public IReadOnlyList<RawProcess> Enumerate() => Next;
    }

    [Fact]
    public void FirstSample_ReturnsEmpty()
    {
        var stub = new StubEnumerator
        {
            Next = new[] { new RawProcess(1, "p", TimeSpan.FromSeconds(10), 1024 * 1024) },
        };
        var clock = DateTimeOffset.UnixEpoch;
        var sampler = new ProcessSampler(stub, logicalCores: 24);
        sampler.Sample(clock).Should().BeEmpty();
    }

    [Fact]
    public void SecondSample_ComputesNormalizedCpuPercent()
    {
        var stub = new StubEnumerator
        {
            Next = new[] { new RawProcess(1, "p", TimeSpan.FromSeconds(10), 0) },
        };
        var sampler = new ProcessSampler(stub, logicalCores: 4);
        var t0 = DateTimeOffset.UnixEpoch;
        sampler.Sample(t0);

        stub.Next = new[] { new RawProcess(1, "p", TimeSpan.FromSeconds(11), 0) };
        var result = sampler.Sample(t0.AddSeconds(1));
        var only = result.Should().ContainSingle().Subject;
        only.CpuPercent.Should().BeApproximately(25, 0.01);
    }

    [Fact]
    public void ProcessExitedBetweenSamples_DroppedFromResults()
    {
        var stub = new StubEnumerator
        {
            Next = new[]
            {
                new RawProcess(1, "p1", TimeSpan.FromSeconds(10), 0),
                new RawProcess(2, "p2", TimeSpan.FromSeconds(10), 0),
            },
        };
        var sampler = new ProcessSampler(stub, logicalCores: 4);
        var t0 = DateTimeOffset.UnixEpoch;
        sampler.Sample(t0);
        stub.Next = new[] { new RawProcess(1, "p1", TimeSpan.FromSeconds(11), 0) };
        var res = sampler.Sample(t0.AddSeconds(1));
        res.Select(p => p.Name).Should().BeEquivalentTo(new[] { "p1" });
    }

    [Fact]
    public void RamMbIsBytesDividedByMb()
    {
        var stub = new StubEnumerator
        {
            Next = new[] { new RawProcess(1, "p", TimeSpan.FromSeconds(10), 200L * 1024 * 1024) },
        };
        var sampler = new ProcessSampler(stub, logicalCores: 4);
        sampler.Sample(DateTimeOffset.UnixEpoch);
        stub.Next = new[] { new RawProcess(1, "p", TimeSpan.FromSeconds(10), 200L * 1024 * 1024) };
        var res = sampler.Sample(DateTimeOffset.UnixEpoch.AddSeconds(1)).Single();
        res.RamMb.Should().BeApproximately(200, 0.1);
    }
}
