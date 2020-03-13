﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.UI.Chat;
using TerraScience.Content.TileEntities;
using TerraScience.Utilities;

namespace TerraScience.Content.Tiles.Multitiles{
	public class SaltExtractor : ModTile{
		public override void SetDefaults(){
			Main.tileNoAttach[Type] = true;
			Main.tileFrameImportant[Type] = true;

			TileObjectData.newTile.CoordinateHeights = new[]{ 16, 16, 16 };
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.Height = 3;
			TileObjectData.newTile.Width = 4;
			TileObjectData.newTile.CoordinatePadding = 2;
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.newTile.LavaDeath = false;
			TileObjectData.newTile.WaterDeath = false;
			TileObjectData.newTile.LavaPlacement = LiquidPlacement.NotAllowed;
			TileObjectData.newTile.WaterPlacement = LiquidPlacement.NotAllowed;
			TileObjectData.addTile(Type);

			ModTranslation name = CreateMapEntryName();
			name.SetDefault("Salt Extractor");
			AddMapEntry(new Color(0xd1, 0x89, 0x32), name);
		}

		public override bool NewRightClick(int i, int j){
			Tile tile = Main.tile[i, j];
			Point16 pos = new Point16(i - tile.frameX / 18, j - tile.frameY / 18);
			bool interactionHappened = false;

			if(Main.LocalPlayer.HeldItemCanPlaceWater() && MiscUtils.TryGetTileEntity(pos, out SaltExtractorEntity se)){
				se.StoredWater++;

				//Only mess with the player items if the Salt Extractor isn't full
				if(se.StoredWater > SaltExtractorEntity.MaxWater)
					se.StoredWater = SaltExtractorEntity.MaxWater;
				else{
					if(Main.LocalPlayer.HeldItem.type == ItemID.WaterBucket){
						Main.LocalPlayer.HeldItem.stack--;
						Main.LocalPlayer.QuickSpawnItem(ItemID.EmptyBucket);
					}

					se.ReactionInProgress = true;

					//Something happened
					interactionHappened = true;
				}
			}

			// If not holding a water bucket
			if (MiscUtils.TryGetTileEntity(pos, out SaltExtractorEntity _) && !Main.LocalPlayer.HeldItemCanPlaceWater()) {
				var terra = ModContent.GetInstance<TerraScience>();
				terra.saltExtracterLoader.ShowUI(terra.saltExtracterLoader.saltExtractorUI);
			}

			return interactionHappened;
		}

		public override bool PreDraw(int i, int j, SpriteBatch spriteBatch){
			//Draw the water in the side vials
			Point16 frame = Main.tile[i, j].TileCoord();
			Point16 pos = new Point16(i, j);

			//Only draw extra stuff when this tile is the upper-left one
			if(frame.X != 0 || frame.Y != 0)
				return true;

			int maxWaterDrawDiff = 34;
			if(MiscUtils.TryGetTileEntity(pos, out SaltExtractorEntity se)){
				//Draw some text if the mouse is hovering over the multitile
				Rectangle multitile = new Rectangle(i * 16, j * 16, TileUtils.Structures.SaltExtractor.GetLength(1) * 16, TileUtils.Structures.SaltExtractor.GetLength(0) * 16);
				if(multitile.Contains(Main.MouseWorld.ToPoint())){
					Vector2 size = ChatManager.GetStringSize(Main.fontMouseText, "l", new Vector2(1f));
					float scale = 0.75f;

					ChatManager.DrawColorCodedString(Main.spriteBatch, Main.fontMouseText, $"Water: {se.StoredWater :N3}L/{SaltExtractorEntity.MaxWater :N3}L", Main.MouseScreen, Color.White, 0f, Vector2.Zero, new Vector2(scale));
					ChatManager.DrawColorCodedString(Main.spriteBatch, Main.fontMouseText, $"Salt: {se.StoredSalt :N3}g", Main.MouseScreen + scale * new Vector2(0, size.Y), Color.White, 0f, Vector2.Zero, new Vector2(scale));
					ChatManager.DrawColorCodedString(Main.spriteBatch, Main.fontMouseText, $"Progress: {(int)(se.ReactionProgress * 100) :N3}%", Main.MouseScreen + scale * 2 * new Vector2(0, size.Y), Color.White, 0f, Vector2.Zero, new Vector2(scale));
					ChatManager.DrawColorCodedString(Main.spriteBatch, Main.fontMouseText, $"Reaction Speed: {se.ReactionSpeed :N3}x", Main.MouseScreen + scale * 3 * new Vector2(0, size.Y), Color.White, 0f, Vector2.Zero, new Vector2(scale));
				}

				//Do the rest of the things
				float curWaterRatio = se.StoredWater / SaltExtractorEntity.MaxWater;
				float invRatio = 1f - curWaterRatio;
				Vector2 offset = new Vector2(2, 46 - maxWaterDrawDiff * invRatio);
				Point drawPos = (pos.ToVector2() * 16 - Main.screenPosition + MiscUtils.ScreenCenter() + offset).ToPoint();

				//Draw the first water bar
				spriteBatch.Draw(Main.magicPixel, new Rectangle(drawPos.X, drawPos.Y, 8, (int)(curWaterRatio * maxWaterDrawDiff)), null, Color.CornflowerBlue, 0f, Vector2.Zero, SpriteEffects.None, 0);

				drawPos.X += 52;

				//Draw the second water bar
				spriteBatch.Draw(Main.magicPixel, new Rectangle(drawPos.X, drawPos.Y, 8, (int)(curWaterRatio * maxWaterDrawDiff)), null, Color.CornflowerBlue, 0f, Vector2.Zero, SpriteEffects.None, 0);
			}

			return true;
		}

		public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem){
			//Do things only when the tile is destroyed and its actually this tile
			Point16 mouse = Main.MouseWorld.ToTileCoordinates16();
			Tile tile = Main.tile[i, j];
			int tileX = tile.frameX / 18;
			int tileY = tile.frameY / 18;

			int columns = TileUtils.Structures.SaltExtractor.GetLength(1);
			int rows = TileUtils.Structures.SaltExtractor.GetLength(0);

			//Only run this code if the tile at the mouse is the same one as (i, j) and the tile is actually being destroyed
			if(!fail && i == mouse.X && j == mouse.Y){
				noItem = true;

				//Determine which tile in the structure was removed and place the others
				Tile structureTile = TileUtils.Structures.SaltExtractor[tileY, tileX];
				int itemType = 0;

				//Determine the dropped item type
				switch(structureTile.type){
					case TileID.CopperPlating:
						itemType = ItemID.CopperPlating;
						break;
					case TileID.TinPlating:
						itemType = ItemID.TinPlating;
						break;
					case TileID.Glass:
						itemType = ItemID.Glass;
						break;
					case TileID.GrayBrick:
						itemType = ItemID.GrayBrick;
						break;
				}

				//Spawn the item
				Item.NewItem(i * 16, j * 16, 16, 16, itemType);

				//Replace the other tiles
				for(int c = 0; c < columns; c++){
					for(int r = 0; r < rows; r++){
						//Only replace the tile if it's not this one
						if(r != tileY || c != tileX){
							Tile newTile = Main.tile[i - tileX + c, j - tileY + r];
							newTile.CopyFrom(TileUtils.Structures.SaltExtractor[r, c]);
							newTile.active(true);
						}
					}
				}
			}

			//Only run this code on the last tile in the structure
	//		if(tileX == columns - 1 && tileY == rows - 1){
				//Update the frames for the tiles
				int minX = i - tileX - 2;
				int minY = j - tileY - 2;
				int sizeX = tileX + 4;
				int sizeY = tileY + 4;
				WorldGen.RangeFrame(minX, minY, sizeX, sizeY);
				//...and send a net message
				if(Main.netMode == NetmodeID.MultiplayerClient)
					NetMessage.SendTileRange(-1, minX, minY, sizeX, sizeY);
	//		}

			//Only run this code on the top-left tile
	//		if(tileX == 0 && tileY == 0){
				//If there's a SaltExtractorEntity present, kill it
				SaltExtractorEntity se = ModContent.GetInstance<SaltExtractorEntity>();
				if(se.Find(i, j) >= 0)
					se.Kill(i, j);
	//		}
		}
	}
}
