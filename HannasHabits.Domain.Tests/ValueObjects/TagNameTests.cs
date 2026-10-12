using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.ValueObjects;

public class TagNameTests
{
    [Fact]
    public void Create_KeepsTheSpellingThePersonChose()
    {
        Assert.Equal("Urlaub am Meer", TagName.Create("Urlaub am Meer").Value);
    }

    [Fact]
    public void Create_TrimsSurroundingWhitespace()
    {
        Assert.Equal("krank", TagName.Create("  \tkrank \n").Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_RejectsAMissingOrBlankName(string? value)
    {
        Assert.Throws<DomainException>(() => TagName.Create(value));
    }

    [Fact]
    public void Create_AcceptsTheMaximumLength()
    {
        var name = new string('a', TagName.MaxLength);

        Assert.Equal(name, TagName.Create(name).Value);
    }

    [Fact]
    public void Create_RejectsOneCharacterTooMany()
    {
        Assert.Throws<DomainException>(() => TagName.Create(new string('a', TagName.MaxLength + 1)));
    }

    [Fact]
    public void Create_CountsTheLengthAfterTrimming()
    {
        var padded = "  " + new string('a', TagName.MaxLength) + "  ";

        Assert.Equal(TagName.MaxLength, TagName.Create(padded).Value.Length);
    }

    [Fact]
    public void TheMaximumLengthIs30()
    {
        // The contract the frontend and the database column are written against.
        Assert.Equal(30, TagName.MaxLength);
    }

    // ---- the key: what names are compared by ----

    [Theory]
    [InlineData("krank", "KRANK")]
    [InlineData("krank", "Krank")]
    [InlineData("Urlaub", "uRLAUB")]
    [InlineData("Ärger", "ärger")]       // not only ASCII
    [InlineData("  krank ", "krank")]
    public void NamesThatDifferOnlyInCase_HaveTheSameKey(string first, string second)
    {
        Assert.Equal(TagName.Create(first).Key, TagName.Create(second).Key);
    }

    [Theory]
    [InlineData("krank", "kranke")]
    [InlineData("bad sleep", "badsleep")]
    [InlineData("Sport", "Sport ")] // trailing space is trimmed: the same name
    public void OtherNames_HaveOtherKeys_ExceptTrimming(string first, string second)
    {
        var sameAfterTrimming = first.Trim() == second.Trim();

        Assert.Equal(sameAfterTrimming, TagName.Create(first).Key == TagName.Create(second).Key);
    }

    [Fact]
    public void TheKey_DoesNotDependOnTheCurrentCulture()
    {
        // The Turkish dotted/dotless i: a culture-sensitive upper-casing would turn "i" into "İ".
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");

            Assert.Equal("KRANKHEIT", TagName.Create("krankheit").Key);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    // ---- equality stays exact ----

    [Fact]
    public void NamesWithTheSameText_AreEqual()
    {
        var first = TagName.Create("krank");
        var second = TagName.Create("  krank ");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void NamesThatDifferInCase_AreNotEqualAsValues_SoACaseOnlyRenameIsAChange()
    {
        // Equal names would make EF Core's change tracking (and any "did it change?" check) miss "krank" -> "Krank".
        Assert.NotEqual(TagName.Create("krank"), TagName.Create("Krank"));
    }

    [Fact]
    public void ToString_IsTheName()
    {
        Assert.Equal("krank", TagName.Create("krank").ToString());
    }
}
