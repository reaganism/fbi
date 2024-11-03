using System;

using Reaganism.FBI.Textual.Fuzzy;
using Reaganism.FBI.Utilities;

namespace Reaganism.FBI.Tests;

[TestFixture]
public static class DiffLineTests
{
    [Test]
    public static void DeleteTest()
    {
        const string text = "Test";

        var diff = new FuzzyDiffLine(FuzzyOperation.DELETE, Utf16String.FromSpan(text.AsSpan()));

        Assert.Multiple(
            () =>
            {
                Assert.That(diff.Operation, Is.EqualTo(FuzzyOperation.DELETE));
                // Assert.That(diff.Text,       Is.EqualTo(text));
                Assert.That(diff.ToString(), Is.EqualTo($"-{text}"));
            }
        );
    }

    [Test]
    public static void InsertTest()
    {
        const string text = "Test";

        var diff = new FuzzyDiffLine(FuzzyOperation.INSERT, Utf16String.FromSpan(text.AsSpan()));

        Assert.Multiple(
            () =>
            {
                Assert.That(diff.Operation, Is.EqualTo(FuzzyOperation.INSERT));
                // Assert.That(diff.Text,       Is.EqualTo(text));
                Assert.That(diff.ToString(), Is.EqualTo($"+{text}"));
            }
        );
    }

    [Test]
    public static void EqualsTest()
    {
        const string text = "Test";

        var diff = new FuzzyDiffLine(FuzzyOperation.EQUALS, Utf16String.FromSpan(text.AsSpan()));

        Assert.Multiple(
            () =>
            {
                Assert.That(diff.Operation, Is.EqualTo(FuzzyOperation.EQUALS));
                // Assert.That(diff.Text,       Is.EqualTo(text));
                Assert.That(diff.ToString(), Is.EqualTo($" {text}"));
            }
        );
    }
}