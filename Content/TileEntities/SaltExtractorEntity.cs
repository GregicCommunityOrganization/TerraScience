﻿using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TerraScience.Content.TileEntities{
	public class SaltExtractorEntity : ModTileEntity, TagSerializable{
		/// <summary>
		/// How much water is stored in the Salt Extractor in Liters.
		/// </summary>
		public float StoredWater = 0f;
		/// <summary>
		/// How much salt is stored in the Salt Extractor.
		/// Once this reaches 1f, a Sodium Chloride item and H2O gas is spawned.
		/// Conversion rate is 2L of water per Sodium Chloride generated.
		/// </summary>
		public float StoredSalt = 0f;
		public static readonly float MaxWater = 10f;
		/// <summary>
		/// How quickly the water is evaporated and salt is created.
		/// </summary>
		public float ReactionSpeed = 1f;
		/// <summary>
		/// The progress for the current reaction.
		/// Range: [0, 1]
		/// </summary>
		public float ReactionProgress = 0f;

		public bool ReactionInProgress = false;

		public TagCompound SerializeData()
			=> new TagCompound(){
				["water"] = StoredWater,
				["salt"] = StoredSalt,
				["speed"] = ReactionSpeed,
				["progress"] = ReactionProgress,
				["doReaction"] = ReactionInProgress
			};

		//The spawn and despawn code is handled elsewhere, so just return true
		public override bool ValidTile(int i, int j)
			=> true;

		public override void Update(){
			if(ReactionInProgress){
				float litersLostPerSecond = 0.05f;
				float reaction = ReactionSpeed * litersLostPerSecond / 60f;
				StoredWater -= reaction;
				StoredSalt += reaction / 2f;

				if(StoredSalt >= 1f){
					StoredSalt--;

					TerraScience.SpawnScienceItem(Position.X * 16 + 32, Position.Y * 16 + 24, 16, 16, "SodiumChloride");
					TerraScience.SpawnScienceItem(Position.X * 16 + 48, Position.Y * 16 + 8, 16, 16, "Water", 1, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2.25f, -4f)));
				}

				//Reaction happens faster and faster as it "heats up", but cools down very quickly
				ReactionSpeed *= 1f + 1.15f / 60f;
			}else{
				ReactionSpeed *= 1f - 3.75f / 60f;

				if(ReactionSpeed < 1f)
					ReactionSpeed = 1f;
			}
		}
	}
}
