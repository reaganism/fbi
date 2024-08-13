namespace Reaganism.FBI.Tests;

[TestFixture]
public static class PatchParsingTests
{
    private const string equality_test_one =
        """
        --- src/decompiled_gog\Terraria.Social\SocialAPI.cs
        +++ src/decompiled\Terraria.Social\SocialAPI.cs
        @@ -34,6 +_,7 @@
         		if (!mode.HasValue)
         		{
         			mode = new SocialMode?(SocialMode.None);
        +			mode = new SocialMode?(SocialMode.Steam);
         		}
         		SocialAPI._mode = mode.Value;
         		SocialAPI._modules = new List<ISocialModule>();

        """;

    [TestCase(equality_test_one)]
    public static void TestTextInputEquality(string text)
    {
        // Not necessary with raw strings.
        // text = text.Trim();

        var patchFile = PatchFile.FromText(text);
        var output    = patchFile.ToString(true);

        Assert.That(output, Is.EqualTo(text));
    }
}