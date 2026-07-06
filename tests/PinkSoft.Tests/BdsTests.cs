using PinkSoft.BDS;
using Xunit;

namespace PinkSoft.Tests;

public class HomographyCalculatorTests
{
    [Fact]
    public void IdentityCorners_MapToSameRegion()
    {
        var src = new double[4, 2] { { 0, 0 }, { 1, 0 }, { 1, 1 }, { 0, 1 } };
        var dst = new double[4, 2] { { 0, 0 }, { 1, 0 }, { 1, 1 }, { 0, 1 } };
        var h = HomographyCalculator.Compute(src, dst);
        var (u, v) = HomographyCalculator.Transform(h, 0.5f, 0.5f);
        Assert.InRange(u, 0.49f, 0.51f);
        Assert.InRange(v, 0.49f, 0.51f);
    }
}

public class LidarBulletFilterTests
{
    [Fact]
    public void TransientPoint_TriggersDetection()
    {
        var filter = new LidarBulletFilter();
        int hits = 0;
        filter.OnBulletDetected += _ => hits++;

        filter.ProcessPoint(new LidarScanPoint(10f, 500f, 20, 0));
        filter.EndScanFrame();
        filter.EndScanFrame();

        Assert.Equal(1, hits);
    }
}
