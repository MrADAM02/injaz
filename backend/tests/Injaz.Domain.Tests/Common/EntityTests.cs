using Injaz.Domain.Common;

namespace Injaz.Domain.Tests.Common;

public sealed class EntityTests
{
    private sealed class Sample : Entity;

    [Fact]
    public void New_entity_gets_a_version_7_id()
    {
        var entity = new Sample();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal(7, entity.Id.Version);
    }

    [Fact]
    public void Ids_created_later_sort_after_earlier_ones()
    {
        var first = new Sample();
        Thread.Sleep(2); // v7 ordering is millisecond-granular
        var second = new Sample();

        Assert.True(string.CompareOrdinal(first.Id.ToString(), second.Id.ToString()) < 0);
    }
}
