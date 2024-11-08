using Reaganism.FBI.Textual.Fuzzy;

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

    private const string equality_test_two =
        """
        --- src/TerrariaNetCore/Terraria/Map/MapHelper.cs
        +++ src/tModLoader/Terraria/Map/MapHelper.cs
        @@ -6,6 +_,8 @@
         using Microsoft.Xna.Framework;
         using Terraria.ID;
         using Terraria.IO;
        +using Terraria.ModLoader;
        +using Terraria.ModLoader.IO;
         using Terraria.Social;
         using Terraria.Utilities;
         
        @@ -134,7 +_,8 @@
         	private static ushort dirtPosition;
         	private static ushort rockPosition;
         	private static ushort hellPosition;
        +	internal static ushort modPosition; // Added by TML.
        -	private static Color[] colorLookup;
        +	internal static Color[] colorLookup;
         	private static ushort[] snowTypes;
         	private static ushort wallRangeStart;
         	private static ushort wallRangeEnd;
        @@ -1604,6 +_,7 @@
         
         		hellPosition = num20;
         		colorLookup[num20] = color10;
        +		modPosition = (ushort)(num20 + 1); // Added by TML.
         		snowTypes = new ushort[6];
         		snowTypes[0] = tileLookup[147];
         		snowTypes[1] = tileLookup[161];
        @@ -1749,7 +_,7 @@
         				int num5 = tile.liquidType();
         				num3 = liquidPosition + num5;
         			}
        -			else if (!tile.invisibleWall() && tile.wall > 0 && tile.wall < WallID.Count) {
        +			else if (!tile.invisibleWall() && tile.wall > 0 && tile.wall < WallLoader.WallCount) {
         				int wall = tile.wall;
         				num3 = wallLookup[wall];
         				num = tile.wallColor();
        @@ -1778,6 +_,7 @@
         		if (num3 == 0) {
         			if ((double)j < Main.worldSurface) {
         				if (Main.remixWorld) {
        +					// Patch note: num2, used below.
         					num2 = 5;
         					num3 = 100;
         				}
        @@ -1821,12 +_,18 @@
         
         				num3 = ((!((double)j < Main.rockLayer)) ? (rockPosition + b) : (dirtPosition + b));
         			}
        +			// Extra patch context.
         			else {
         				num3 = hellPosition;
         			}
         		}
         
        +		/*
         		return MapTile.Create((ushort)(num3 + baseOption), (byte)num2, (byte)num);
        +		*/
        +		ushort mapType = (ushort)(num3 + baseOption);
        +		MapLoader.ModMapOption(ref mapType, i, j);
        +		return MapTile.Create(mapType, (byte)num2, (byte)num);
         	}
         
         	public static void GetTileBaseOption(int x, int y, int tileType, Tile tileCache, ref int baseOption)
        @@ -2603,7 +_,10 @@
         					byte b6 = 0;
         					int num7;
         					ushort num8;
        +					/*
         					if (mapTile.Light <= 18) {
        +					*/
        +					if (mapTile.Light <= 18 || mapTile.Type >= modPosition) {
         						flag2 = false;
         						flag = false;
         						num7 = 0;
        @@ -2761,6 +_,8 @@
         
         			deflateStream.Dispose();
         			FileUtilities.WriteAllBytes(text, memoryStream.ToArray(), isCloudSave);
        +
        +			MapIO.WriteModFile(text, isCloudSave);
         		}
         
         		noStatusText = false;
        @@ -2768,6 +_,7 @@
         
         	public static void LoadMapVersion1(BinaryReader fileIO, int release)
         	{
        +		Main.MapFileMetadata = FileMetadata.FromCurrentSettings(FileType.Map); // TML fix. Fix map saving if loading old map file.
         		string text = fileIO.ReadString();
         		int num = fileIO.ReadInt32();
         		int num2 = fileIO.ReadInt32();

        """;

    [TestCase(equality_test_one)]
    [TestCase(equality_test_two)]
    public static void TestTextInputEquality(string text)
    {
        // Not necessary with raw strings.
        // text = text.Trim();

        var patchFile = FuzzyPatchFile.FromText(text);
        var output    = patchFile.ToString(true);

        Assert.That(output, Is.EqualTo(text));
    }
}