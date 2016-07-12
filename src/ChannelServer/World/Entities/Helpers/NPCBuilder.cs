using Aura.Channel.Network.Sending;
using Aura.Channel.Skills;
using Aura.Channel.World.Entities.Creatures;
using Aura.Channel.World.Inventory;
using Aura.Data;
using Aura.Data.Database;
using Aura.Mabi.Const;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.Channel.World.Entities.Helpers
{
	public abstract class NPCBuilder : NPC
	{
		string _baseName = "";
		string _npcName = "";
		public string BaseName { get { return _baseName; } set { _baseName = value; UpdateName(); } }
		public string NpcName { get { return _npcName; } set { _npcName = value; UpdateName(); } }

		private void UpdateName()
		{
			if (BaseName == "")
				this.Name = NpcName;
			else if (NpcName == "")
				this.Name = BaseName;
			else
			{
				var npcName = _npcName.TrimStart(new char[]{'_'});
				this.Name = string.Format("{0} ({1})", npcName, _baseName);
			}
		}

		protected NPCBuilder()
		{
			//Some defaults, so we won't end up with undefined values
			this.RaceId = 0;
			this.Age = 18;
			this.Level = 1;
			SetVitals(10, 10, 10);
			SetBaseStats(0, 0, 0, 0, 0);

			this.OnNPCLoggedIn += this.OnLoggedIn;
		}

		public void SetRace(int raceId)
		{
			Log.Debug("RaceId = {0}, raceId = {1}", this.RaceId, raceId);
			if (raceId == 0)
				throw new Exception("NPCBuilder: raceId for SetRace cannot be 0!");

			// if race was already set
			if (this.RaceId != 0)
			{
				// A failsafe method of replacing character data
				var raceData = AuraData.RaceDb.Find(this.RaceId);
				if (raceData == null)
				{
					// Try to default to Human
					raceData = AuraData.RaceDb.Find(10000);
					if (raceData == null)
						throw new Exception("Unable to load race data, race '" + this.RaceId.ToString() + "' not found.");

					Log.Warning("Creature.LoadDefault: Race '{0}' not found, using human instead.", this.RaceId);
				}
				if(raceData != null)
				{
					this.RaceId = raceId;
					this.RaceData = raceData;
				}
			}
			// setting race for the first time
			else
			{
				this.RaceId = raceId;
				LoadDefault();
			}
		}

		public void SetAppearance(ActorData actorData)
		{
			if (this.RaceId != actorData.RaceId)
				SetRace(actorData.RaceId);

			if (actorData.HasColors)
			{
				this.Color1 = actorData.Color1;
				this.Color2 = actorData.Color2;
				this.Color3 = actorData.Color3;
			}

			this.Weight = actorData.Weight;
			this.Height = actorData.Height;
			this.Upper = actorData.Upper;
			this.Lower = actorData.Lower;
			this.EyeColor = (byte)actorData.EyeColor;
			this.EyeType = (byte)actorData.EyeType;
			this.MouthType = (byte)actorData.MouthType;
			this.SkinColor = (byte)actorData.SkinColor;

			if (actorData.FaceItemId != 0)
			{
				var face = new Item(actorData.FaceItemId);
				face.Info.Color1 = (byte)actorData.SkinColor;
				this.Inventory.Add(face, Pocket.Face);
			}
			if (actorData.HairItemId != 0)
			{
				var hair = new Item(actorData.HairItemId);
				hair.Info.Color1 = actorData.HairColor;
				this.Inventory.Add(hair, Pocket.Hair);
			}
		}

		public void SetAppearance(Creature creature)
		{
			if (this.RaceId != creature.RaceId)
				SetRace(creature.RaceId);

			this.Color1 = creature.Color1;
			this.Color2 = creature.Color2;
			this.Color3 = creature.Color3;

			this.Weight = creature.Weight;
			this.Height = creature.Height;
			this.Upper = creature.Upper;
			this.Lower = creature.Lower;
			this.EyeColor = creature.EyeColor;
			this.EyeType = creature.EyeType;
			this.MouthType = creature.MouthType;
			this.SkinColor = creature.SkinColor;

			var face1 = creature.Inventory.GetItemAt(Pocket.Face, 0, 0);
			if (face1 != null)
			{
				var face = new Item(face1.Info.Id);
				face.Info.Color1 = face1.Info.Color1;
				this.Inventory.Add(face, Pocket.Face);
			}
			var hair1 = creature.Inventory.GetItemAt(Pocket.Hair, 0, 0);
			if (hair1 != null)
			{
				var hair = new Item(hair1.Info.Id);
				hair.Info.Color1 = hair1.Info.Color1;
				this.Inventory.Add(hair, Pocket.Hair);
			}
		}

		public void SetAppearance(int faceId, int hairId, uint hairColor, int eyeColor, int eyeType, int mouthType, int skinColor, float weight = 1.0f, float height = 1.0f, float upper = 1.0f, float lower = 1.0f, uint color1 = 0x808080, uint color2 = 0x808080, uint color3 = 0x808080)
		{
			this.Weight = weight;
			this.Height = height;
			this.Upper = upper;
			this.Lower = lower;

			this.EyeColor = (byte)eyeColor;
			this.EyeType = (byte)eyeType;
			this.MouthType = (byte)mouthType;
			this.SkinColor = (byte)skinColor;

			this.Color1 = color1;
			this.Color2 = color2;
			this.Color3 = color3;

			if (faceId != 0)
			{
				var face = new Item(faceId);
				face.Info.Color1 = (byte)skinColor;
				this.Inventory.Add(face, Pocket.Face);
			}
			if (hairId != 0)
			{
				var hair = new Item(hairId);
				hair.Info.Color1 = hairColor;
				this.Inventory.Add(hair, Pocket.Hair);
			}
		}

		public void SetVitals(float lifeMax, float manaMax, float staminaMax, bool fullHeal = true, float life = 5.0f, float mana = 0.0f, float stamina = 0.0f)
		{
			this.LifeMaxBase = lifeMax;
			this.ManaMaxBase = manaMax;
			this.StaminaMaxBase = staminaMax;
			this.Life = fullHeal ? this.LifeMax : life;
			this.Mana = fullHeal ? this.ManaMax : mana;
			this.Stamina = fullHeal ? this.StaminaMax : stamina;
		}

		public void SetBaseStats(float str, float int_, float dex, float will, float luck)
		{
			this.StrBase = str;
			this.IntBase = int_;
			this.DexBase = dex;
			this.WillBase = will;
			this.LuckBase = luck;
		}

		public void SetLevel(short level)
		{
			this.Level = level;
			this.Exp = level > 1 ? AuraData.ExpDb.GetTotalForNextLevel(level - 1) : 0;
		}

		public void SetEquipment(Pocket pocket, Item item)
		{
			item.Info.Pocket = pocket;
			this.Inventory.Add(item, pocket);
		}

		public void SetEquipment(ActorData actorData)
		{
			foreach (var itemData in actorData.Items)
			{
				var item = new Item(itemData.ItemId);

				if (itemData.HasColors)
				{
					item.Info.Color1 = itemData.Color1;
					item.Info.Color2 = itemData.Color2;
					item.Info.Color3 = itemData.Color3;
				}

				var pocket = (Pocket)itemData.Pocket;
				if (pocket != Pocket.None)
					SetEquipment(pocket, item);
			}
		}

		public void SetEquipment(Creature creature)
		{
			foreach (var src in creature.Inventory.GetAllEquipment())
			{
				var item = new Item(src.Info.Id);

				item.Info.Color1 = src.Info.Color1;
				item.Info.Color2 = src.Info.Color2;
				item.Info.Color3 = src.Info.Color3;

				item.Info.Pocket = src.Info.Pocket;
				SetEquipment(src.Info.Pocket, item);
			}
		}

		public void GiveSkill(SkillId id, SkillRank rank = SkillRank.Novice)
		{
			for (byte b = (byte)SkillRank.Novice; b <= (byte)rank; b++)
			{
				this.Skills.Give(id, (SkillRank)b);
			}
		}

		private void OnLoggedIn()
		{
			try
			{
				GiveSkill(SkillId.CombatMastery, SkillRank.Novice);

				this.OnPreUpdate();
				if (this.RaceId == 0 || this.RaceData == null)
					throw new Exception("Race has to be set in OnPreUpdate!");
				Send.StatUpdateDefault(this);

				this.OnEquipmentUpdate();
				this.OnInventoryUpdate();

				this.OnSkillsUpdate();

				this.OnPostUpdate();
				Send.StatUpdate(this, StatUpdateType.Public,
					Stat.Weight, Stat.Height, Stat.Upper, Stat.Lower,
					Stat.CombatPower,
					Stat.Life, Stat.LifeMax, Stat.LifeMaxMod, Stat.LifeInjured
				);
				Send.StatUpdate(this, StatUpdateType.Private,
					Stat.Life, Stat.LifeMax, Stat.Mana, Stat.Stamina,
					Stat.FoodMinRatio
				);

				GiveSkill(SkillId.NormalAttack);
			}
			catch (Exception e)
			{
				Log.Error("NPCBuilder: Exception during NPC OnLoggedIn update.");
				Log.Exception(e);
				return;
			}
		}

		/// <summary>
		/// Initial updates. Setting up race, age, level, general appearance, etc.
		/// The only required element.
		/// </summary>
		/// <remarks>
		/// Recommended methods:
		/// SetRace, SetLevel, SetAppearance
		/// </remarks>
		protected abstract void OnPreUpdate();

		/// <summary>
		/// Setting up parts of equipment.
		/// as templates.
		/// </summary>
		/// <remarks>
		/// Recommended methods:
		/// SetEquipment
		/// </remarks>
		protected virtual void OnEquipmentUpdate() { }

		/// <summary>
		/// Add and remove items in inventory.
		/// </summary>
		/// <remarks>
		/// Recommended methods:
		/// GiveItem
		/// </remarks>
		protected virtual void OnInventoryUpdate() { }

		/// <summary>
		/// Add all the necessary skills.
		/// </summary>
		/// <remarks>
		/// Recommended methods:
		/// SetSkill
		/// </remarks>
		protected virtual void OnSkillsUpdate() { }

		/// <summary>
		/// Any additional initialization needed,
		/// like updating or overriding base stats.
		/// </summary>
		/// <remarks>
		/// Recommended methods:
		/// SetVitals, SetBaseStats
		/// </remarks>
		protected virtual void OnPostUpdate() { }
	}
}
