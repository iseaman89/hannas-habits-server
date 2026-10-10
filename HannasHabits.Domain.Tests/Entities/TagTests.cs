using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.Entities;

public class TagTests
{
    private static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void Create_SetsOwnerNameAndKey()
    {
        var tag = Tag.Create(User, TagName.Create("Krank"), tagsOfUser: 0);

        Assert.Equal(User, tag.UserId);
        Assert.Equal("Krank", tag.Name.Value);
        Assert.Equal("KRANK", tag.NameKey);
        Assert.NotEqual(Guid.Empty, tag.Id);
    }

    [Fact]
    public void Create_AcceptsTheLastTagBelowTheLimit()
    {
        var tag = Tag.Create(User, TagName.Create("x"), Tag.MaxPerUser - 1);

        Assert.NotNull(tag);
    }

    [Theory]
    [InlineData(Tag.MaxPerUser)]
    [InlineData(Tag.MaxPerUser + 1)]
    public void Create_RejectsATagBeyondTheLimit(int tagsOfUser)
    {
        var exception = Assert.Throws<DomainException>(() => Tag.Create(User, TagName.Create("x"), tagsOfUser));

        Assert.Contains(Tag.MaxPerUser.ToString(), exception.Message);
    }

    [Fact]
    public void TheLimitIs200()
    {
        Assert.Equal(200, Tag.MaxPerUser);
    }

    [Fact]
    public void Rename_ChangesNameAndKeyTogether()
    {
        var tag = Tag.Create(User, TagName.Create("krank"), 0);

        tag.Rename(TagName.Create("Erkältet"));

        Assert.Equal("Erkältet", tag.Name.Value);
        Assert.Equal("ERKÄLTET", tag.NameKey);
    }

    [Fact]
    public void Rename_CanChangeOnlyTheSpelling()
    {
        var tag = Tag.Create(User, TagName.Create("krank"), 0);

        tag.Rename(TagName.Create("Krank"));

        Assert.Equal("Krank", tag.Name.Value);
        Assert.Equal("KRANK", tag.NameKey);
    }
}
