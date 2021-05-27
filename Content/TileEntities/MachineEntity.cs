﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TerraScience.Content.TileEntities.Energy.Generators;
using TerraScience.Content.Tiles;
using TerraScience.Content.UI;
using TerraScience.Systems;
using TerraScience.Utilities;

namespace TerraScience.Content.TileEntities{
	public abstract class MachineEntity : ModTileEntity{
		private readonly List<Item> slots = new List<Item>();

		public Item GetItem(int slot) => slots[slot];

		internal void ValidateSlots(int intendedLength){
			//Possible if the multitile was just placed
			if(slots.Count != intendedLength){
				slots.Clear();
				for(int i = 0; i < intendedLength; i++){
					Item item = new Item();
					slots.Add(item);
				}
			}
		}

		/// <summary>
		/// The multiplier for the reaction progress.
		/// </summary>
		public float ReactionSpeed = 1f;

		/// <summary>
		/// The progress for the current reaction.
		/// Range: [0, 100]
		/// </summary>
		public float ReactionProgress = 0f;

		public bool ReactionInProgress = false;

		public MachineUI ParentState;

		public string MachineName;

		public abstract int MachineTile{ get; }

		public abstract int SlotsCount{ get; }

		public sealed override bool ValidTile(int i, int j){
			Tile tile = Framing.GetTileSafely(i, j);
			return tile.active() && tile.type == MachineTile && tile.frameX == 0 && tile.frameY == 0;
		}

		public virtual void PreUpdateReaction(){ }

		/// <summary>
		/// Update <seealso cref="ReactionProgress"/> here.  Return true to indicate that a reaction is supposed to happen, false otherwise.
		/// </summary>
		public abstract bool UpdateReaction();

		/// <summary>
		/// Called after <seealso cref="ReactionComplete()"/>, but not necessarily when a reaction is completed.
		/// </summary>
		public virtual void PostUpdateReaction(){ }

		/// <summary>
		/// Actions that should happen when a reaction is complete goes here.
		/// </summary>
		public abstract void ReactionComplete();

		/// <summary>
		/// Always called.  General update task after the reaction has been handled.
		/// </summary>
		public virtual void PostReaction(){ }

		/// <summary>
		/// If this machine must have its UI open in order to update.
		/// </summary>
		public virtual bool RequiresUI => false;

		public sealed override TagCompound Save()
			=> new TagCompound(){
				["machineInfo"] = new TagCompound(){
					[nameof(ReactionSpeed)] = ReactionSpeed,
					[nameof(ReactionProgress)] = ReactionProgress,
					[nameof(ReactionInProgress)] = ReactionInProgress
				},
				["slots"] = new TagCompound(){
					//Lots of unnecessary data is saved, but that's fine due to the small amount of extra bytes used
					// TODO: refactor ItemIO.Save/ItemIO.Load to get rid of this extra info
					["items"] = slots.Count == 0 ? null : slots.Select(i => ItemIO.Save(i)).ToList()
				},
				["extra"] = ExtraSave()
			};

		public virtual TagCompound ExtraSave() => null;

		public sealed override void Load(TagCompound tag){
			TagCompound info = tag.GetCompound("machineInfo");
			ReactionSpeed = info.GetFloat(nameof(ReactionSpeed));
			ReactionProgress = info.GetFloat(nameof(ReactionProgress));
			ReactionInProgress = info.GetBool(nameof(ReactionInProgress));

			TagCompound tagSlots = tag.GetCompound("slots");
			List<TagCompound> items = tagSlots.GetList<TagCompound>("items") as List<TagCompound> ?? new List<TagCompound>();
			
			foreach(var c in items)
				slots.Add(ItemIO.Load(c));

			TagCompound extra = tag.GetCompound("extra");
			if(extra != null)
				ExtraLoad(extra);
		}

		public virtual void ExtraLoad(TagCompound tag){ }

		public sealed override void Update(){
			if(RequiresUI && !(ParentState?.Active ?? false))
				return;

			if(this is GeneratorEntity && !updating)
				return;

			ValidateSlots(SlotsCount);

			PreUpdateReaction();

			if(ReactionInProgress && UpdateReaction()){
				if(ReactionProgress >= 100){
					ReactionComplete();

					//In case a derived class forgets to reset the reaction progress
					if(ReactionProgress >= 100)
						ReactionProgress -= 100;
				}

				PostUpdateReaction();
			}

			PostReaction();
		}

		internal bool updating = false;
		//NOTE: ModTileEntity.PreGlobalUpdate() is called from a singleton for some fucking reason

		public void SaveSlots(){
			slots.Clear();

			for(int i = 0; i < ParentState.SlotsLength; i++){
				int type = ParentState.GetSlot(i).StoredItem.type;
				int stack = ParentState.GetSlot(i).StoredItem.stack;

				Item item = new Item();
				item.SetDefaults(type);
				item.stack = stack;

				slots.Add(item);
			}
		}

		public void LoadSlots(){
			ParentState.LoadToSlots(slots);
		}

		public override void OnKill(){
			//Force the UI to close if it's open
			if(ParentState?.Active ?? false)
				TechMod.Instance.machineLoader.HideUI(MachineName);
		}

		public override void NetSend(BinaryWriter writer, bool lightSend){
			var pair = TileUtils.tileToEntity.First(p => p.Value.GetType() == this.GetType());
			int entity = pair.Key;
			TagIO.WriteTag(TileUtils.tileToStructureName[entity], pair.Value.Save(), writer);
		}

		public override void NetReceive(BinaryReader reader, bool lightReceive){
			Load(TagIO.Read(reader));
		}

		internal SoundEffectInstance PlayCustomSound(Vector2 position, string path){
			bool nearbyMuffler = WorldGen.InWorld((int)position.X >> 4, (int)position.Y >> 4) && MachineMufflerTile.AnyMufflersNearby(position);

			return Main.PlaySound(SoundLoader.customSoundType, (int)position.X, (int)position.Y, TechMod.Instance.GetSoundSlot(SoundType.Custom, $"Sounds/Custom/{path}"), volumeScale: nearbyMuffler ? 0.1f : 1f);
		}

		internal void PlaySound(int type, Vector2 position, int style = 1){
			bool nearbyMuffler = WorldGen.InWorld((int)position.X >> 4, (int)position.Y >> 4) && MachineMufflerTile.AnyMufflersNearby(position);

			Main.PlaySound(type, (int)position.X, (int)position.Y, style, volumeScale: nearbyMuffler ? 0.1f : 1f);
		}

		internal void PlaySound(Terraria.Audio.LegacySoundStyle type, Vector2 position){
			bool nearbyMuffler = WorldGen.InWorld((int)position.X >> 4, (int)position.Y >> 4) && MachineMufflerTile.AnyMufflersNearby(position);

			Main.PlaySound(type.SoundId, (int)position.X, (int)position.Y, type.Style, volumeScale: nearbyMuffler ? 0.1f : 1f);
		}

		internal void PlaySound(int type, int x = -1, int y = -1, int style = 1){
			bool nearbyMuffler = WorldGen.InWorld(x, y) && MachineMufflerTile.AnyMufflersNearby(new Vector2(x, y));

			Main.PlaySound(type, x, y, style, volumeScale: nearbyMuffler ? 0.1f : 1f);
		}

		internal abstract int[] GetInputSlots();

		internal abstract int[] GetOutputSlots();

		/// <summary>
		/// The function the entity should fall back to for detecing if an incoming item is valid when its parent UI state is not active.
		/// You can assume that <paramref name="slot"/> refers to an "input item" slot
		/// </summary>
		internal abstract bool CanInputItem(int slot, Item item);

		public bool CanBeInput(Item item){
			int stack = item.stack;
			int[] inputSlots = GetInputSlots();

			foreach(int slot in inputSlots){
				Item slotItem;
				if((ParentState?.GetSlot(slot).ValidItemFunc(item) ?? false) || CanInputItem(slot, item)){
					slotItem = this.RetrieveItem(slot);

					if(slotItem.IsAir)
						return true;
					else if(slotItem.type == item.type){
						if(slotItem.stack + stack <= slotItem.maxStack)
							return true;
						else
							stack -= slotItem.maxStack - slotItem.stack;
					}
				}
			}

			return stack <= 0;
		}

		public bool TryExtractOutputs(int stackToExtract, out Item item){
			var outputs = GetOutputSlots();
			item = null;

			if(outputs.Length == 0)
				return false;

			//Try to remove items from the machine
			//Should a stack underflow, check if another stack has the same type.  If one does, remove from that stack as well
			//Keep removing from stacks until either 1) all slots have been checked or 2) "stackToExtract" reaches zero
			foreach(int slot in outputs){
				Item slotItem = this.RetrieveItem(slot);

				if(slotItem.IsAir)
					continue;

				if(item is null || slotItem.type == item.type){
					if(item is null){
						//Just use this item directly
						item = slotItem.Clone();
						
						if(item.stack > stackToExtract){
							item.stack = stackToExtract;
							slotItem.stack -= stackToExtract;
							stackToExtract = 0;
						}else{
							stackToExtract -= item.stack;
							slotItem.stack = 0;
						}
					}else{
						//Add to the stack of the output item
						if(slotItem.stack < stackToExtract){
							stackToExtract -= slotItem.stack;
							item.stack += slotItem.stack;
							slotItem.stack = 0;
						}else{
							item.stack += stackToExtract;
							slotItem.stack -= stackToExtract;
							stackToExtract = 0;
						}
					}
				}

				if(stackToExtract <= 0)
					break;
			}

			return item != null;
		}

		public void InputItemFromNetwork(ItemNetworkPath incoming, out bool sendBack){
			Item data = ItemIO.Load(incoming.itemData);
			sendBack = true;

			if(!CanBeInput(data))
				return;

			int[] inputSlots = GetInputSlots();

			foreach(int slot in inputSlots){
				Item slotItem = this.RetrieveItem(slot);
				if(slotItem.IsAir || slotItem.type == data.type){
					if(slotItem.IsAir){
						slots[slot] = data.Clone();

						if(ParentState?.Active ?? false)
							ParentState.LoadToSlots(slots);

						slotItem = this.RetrieveItem(slot);
						slotItem.stack = 0;
					}

					if(slotItem.stack + data.stack > slotItem.maxStack){
						data.stack -= slotItem.maxStack - slotItem.stack;
						slotItem.stack = slotItem.maxStack;
					}else{
						slotItem.stack += data.stack;
						data.stack = 0;

						sendBack = false;
						break;
					}
				}
			}

			if(sendBack)
				incoming.itemData = ItemIO.Save(data);
		}

		public override bool Equals(object obj)
			=> obj is MachineEntity entity && Position == entity.Position;

		public override int GetHashCode() => base.GetHashCode();

		public static bool operator ==(MachineEntity first, MachineEntity second)
			=> first?.Position == second?.Position;

		public static bool operator !=(MachineEntity first, MachineEntity second)
			=> first?.Position != second?.Position;
	}
}
