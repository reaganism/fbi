namespace Reaganism.FBI.Tests;

[TestFixture]
public static class DiffLineTests
{
    [Test]
    public static void DeleteTest()
    {
        const string text = "Test";

        var diff = new DiffLine(Operation.DELETE, text);

        Assert.Multiple(
            () =>
            {
                Assert.That(diff.Operation,  Is.EqualTo(Operation.DELETE));
                Assert.That(diff.Text,       Is.EqualTo(text));
                Assert.That(diff.ToString(), Is.EqualTo($"-{text}"));
            }
        );
    }

    [Test]
    public static void InsertTest()
    {
        const string text = "Test";

        var diff = new DiffLine(Operation.INSERT, text);

        Assert.Multiple(
            () =>
            {
                Assert.That(diff.Operation,  Is.EqualTo(Operation.INSERT));
                Assert.That(diff.Text,       Is.EqualTo(text));
                Assert.That(diff.ToString(), Is.EqualTo($"+{text}"));
            }
        );
    }

    [Test]
    public static void EqualsTest()
    {
        const string text = "Test";

        var diff = new DiffLine(Operation.EQUALS, text);

        Assert.Multiple(
            () =>
            {
                Assert.That(diff.Operation,  Is.EqualTo(Operation.EQUALS));
                Assert.That(diff.Text,       Is.EqualTo(text));
                Assert.That(diff.ToString(), Is.EqualTo($" {text}"));
            }
        );
    }
}