//--- Aura Script -----------------------------------------------------------
// G1 The three missing warriors RP
//--- Description -----------------------------------------------------------
// Normal Alby spawns.
//---- Cutscenes ------------------------------------------------------------
// G1_5_a_3WarriorsRP // On Validation/Creation?
// G1_5_b_3WarriorsRP, 1000, cMemberList // if (floor == 1 && section ==2)
// G1_5_c_3WarriorsRP, 1000, cMemberList // On Clear
// G1_LeaveDungeon // plays when failed?!
//---------------------------------------------------------------------------

[DungeonScript("g1rp_05_tircho_alby_dungeon_gfmainstream2012part1")]
public class AlbyRPDungeonScript : DungeonScript
{
	public override void Load()
	{
		IsRolePlaying = true;
	}
	
	private int GetPartyIndex(Creature creature)
	{
		var leader = creature.Party.Leader;
		if (creature == leader)
			return 0;
		
		int idx = 1;
		foreach (var member in creature.Party.GetSortedMembers())
		{
			if (member == leader)
				continue;
			
			if (member == creature)
				return idx;
			
			idx++;
		}
		
		return idx;
	}
	
	public override Creature OnSubstitutePlayer(Creature player)
	{
		if (player.Party.MemberCount == 1)
		{
			// For solo, RP as Ruairi
			return new RPRuairi(player.Name);
		}
		else
		{
			// For 2-3 members, leader is Tarlach, 2nd player is Ruairi, 3rd player is Mari
			switch(GetPartyIndex(player))
			{
			case 0: return new RPTarlach(player.Name);
			case 1: return new RPRuairi(player.Name);
			case 2: return new RPMari(player.Name);
			default:
				Log.Warning("Too many players tried to enter dungeon!");
				return null;
			}
		}
	}
	
	public override void OnBoss(Dungeon dungeon)
	{
		dungeon.AddBoss(30004, 1); // Giant Spider
		dungeon.AddBoss(30003, 6); // Red Spider

		dungeon.PlayCutscene("bossroom_GiantSpider");
	}
	
	public override void OnPartyEntered(Dungeon dungeon, Creature creature)
	{
		dungeon.PlayCutscene("G1_5_a_3WarriorsRP");
	}
	
	public override void OnSectionCleared(Dungeon dungeon, int floor, int section)
	{
		if (floor==1 && section==2)
			dungeon.PlayCutscene("G1_5_b_3WarriorsRP");
	}
	
	public override void OnLeftEarly(Dungeon dungeon, Creature creature)
	{
		dungeon.PlayCutscene("G1_LeaveDungeon");
	}
	
	public override void OnCleared(Dungeon dungeon)
	{
		dungeon.PlayCutscene("G1_5_c_3WarriorsRP");
		
		var creators = dungeon.GetCreators();

		for (int i = 0; i < creators.Count; ++i)
		{
			var npcMember = member as NPC;
			var controller = (npcMember != null && npcMember.IsRolePlayingNPC) ? npcMember.Temp.RolePlayingController : member;
			if (controller.Quests.IsActive(213004, "clear_rp_alby"))
			{
				controller.Keywords.Give("g1_goddess");
				controller.Keywords.Remove("g1_tarlach2");
				controller.Keywords.Give("g1_04");
				controller.Keywords.Remove("g1_03");
			}
			
			if (npcMember != null && controller is PlayerCreature)
				(controller as PlayerCreature).DisconnectFromNPC();
			else // as a failsafe
				member.Warp(dungeon.Data.Exit);
		}
	}

	List<DropData> drops;
	public Item GetRandomTreasureItem(Random rnd)
	{
		if (drops == null)
		{
			drops = new List<DropData>();
			drops.Add(new DropData(itemId: 62004, chance: 44, amountMin: 1, amountMax: 2)); // Magic Powder
			drops.Add(new DropData(itemId: 51102, chance: 44, amountMin: 1, amountMax: 2)); // Mana Herb
			drops.Add(new DropData(itemId: 71017, chance: 2, amountMin: 1, amountMax: 2));  // White Spider Fomor Scroll
			drops.Add(new DropData(itemId: 71019, chance: 2, amountMin: 1, amountMax: 1)); // Red Spider Fomor Scroll
			drops.Add(new DropData(itemId: 63116, chance: 1, amount: 1, expires: 480)); // Alby Int 1
			drops.Add(new DropData(itemId: 63117, chance: 1, amount: 1, expires: 480)); // Alby Int 2
			drops.Add(new DropData(itemId: 63118, chance: 1, amount: 1, expires: 480)); // Alby Int 4
			drops.Add(new DropData(itemId: 63101, chance: 2, amount: 1, expires: 480)); // Alby Basic
			drops.Add(new DropData(itemId: 40002, chance: 1, amount: 1, color1: 0x000000, durability: 0)); // Wooden Blade (black)

			if (IsEnabled("AlbyAdvanced"))
			{
				drops.Add(new DropData(itemId: 63160, chance: 1, amount: 1, expires: 360)); // Alby Advanced 3-person Fomor Pass
				drops.Add(new DropData(itemId: 63161, chance: 1, amount: 1, expires: 360)); // Alby Advanced Fomor Pass
			}
		}

		return Item.GetRandomDrop(rnd, drops);
	}
}

public class RPTarlach : RolePlayingNPC
{
	public RPTarlach(string playerName)
	{
		NpcName = "Tarlach";
		BaseName = playerName;
	}

	override protected void OnPreUpdate()
	{
		SetRace(10002);
		SetAppearance(
				4901, 4021, // faceId, hairId
				0xDDBB80, // hairColor
				54, 4, 0, 15, // eyeColor, eyeType, mouthType, skinColor
				0.0f, 1.1f, 0.4f, 0.6f // [weight, height, upper, lower]
		);
		this.Age = 19;
		SetLevel(56);
	}

	override protected void OnEquipmentUpdate()
	{
		EquipItem(Pocket.Armor, 15069, 0x00FFFFFF, 0x00FFFFFF, 0x00FFFFFF);
		EquipItem(Pocket.Shoe, 17032, 0x00563211, 0x00FCDCD5, 0x007DA834);
		EquipItem(Pocket.Head, 18028, 0x00625F44, 0x00C0C0C0, 0x00601469);
		EquipItem(Pocket.Robe, 19004, 0x00CA7B34, 0x00B45031, 0x00DABC87);
		EquipItem(Pocket.RightHand2, 40017, 0x00DAC3BC, 0x00AACFC8, 0x00B16B4A);
	}

	override protected void OnInventoryUpdate()
	{
		GiveItem(51002, 10);
		GiveItem(51002, 10);
		GiveItem(51002, 10);
		GiveItem(51012, 10);
		GiveItem(63000, 10);
		GiveItem(51007, 10);
		GiveItem(51007, 10);
		GiveItem(51007, 10);
		GiveItem(51007, 10);
		GiveItem(51007, 10);
		GiveItem(60005, 10);
		GiveItem(60005, 10);
		GiveItem(60005, 10);
		GiveItem(60005, 10);
		GiveItem(91399);
		GiveItem(91399);
	}

	override protected void OnSkillsUpdate()
	{
		GiveSkill(SkillId.PlayingInstrument, SkillRank.RD);
		GiveSkill(SkillId.Rest, SkillRank.RD);
		GiveSkill(SkillId.Composing, SkillRank.RD);
		GiveSkill(SkillId.MusicalKnowledge, SkillRank.RE);
		GiveSkill(SkillId.FirstAid, SkillRank.RE);
		GiveSkill(SkillId.Defense, SkillRank.RD);
		
		// ?
		// This is how the officials handle this
		GiveSkill(SkillId.CriticalHit, SkillRank.Novice);
		GiveSkill(SkillId.CombatMastery, SkillRank.RB);
		GiveSkill(SkillId.CriticalHit, SkillRank.RA);
		
		GiveSkill(SkillId.Meditation, SkillRank.RD);
		GiveSkill(SkillId.Enchant, SkillRank.RD);
		GiveSkill(SkillId.Healing, SkillRank.RA);

		GiveSkill(SkillId.Lightningbolt, SkillRank.R8);
		GiveSkill(SkillId.Firebolt, SkillRank.R7);
		GiveSkill(SkillId.Icebolt, SkillRank.R9);
	}

	override protected void OnPostUpdate()
	{
		// this.LifeMaxMod = 50.0f;
		this.AbilityPoints = 7;
		SetBaseStats(83, 301, 88, 128, 70);
		SetVitals(302.0f, 325.0f, 120.0f);
	}
}

public class RPRuairi : RolePlayingNPC
{
	public RPRuairi(string playerName)
	{
		NpcName = "Ruairi";
		BaseName = playerName;
	}

	override protected void OnPreUpdate()
	{
		SetRace(10002);
		SetAppearance(
				4900, 4029, // faceId, hairId
				0x10000026, // hairColor
				37, 12, 13, 17, // eyeColor, eyeType, mouthType, skinColor
				1.0f, 1.3f, 1.3f, 1.0f // [weight, height, upper, lower]
		);
		this.Age = 19;
		SetLevel(53);
	}

	override protected void OnEquipmentUpdate()
	{
		EquipItem(Pocket.Armor, 13021, 0x00808080, 0x00808080, 0x00808080);
		EquipItem(Pocket.Glove, 16508, 0x00808080, 0x00808080, 0x00808080);
		EquipItem(Pocket.Shoe, 17509, 0x00808080, 0x00808080, 0x00808080);
		EquipItem(Pocket.RightHand1, 40028, 0x00808080, 0x00808080, 0x00808080);
	}

	override protected void OnInventoryUpdate()
	{
		GiveItem(63002, 5);
		GiveItem(63002, 5);
		GiveItem(63002, 5);
		GiveItem(63002, 5);
		GiveItem(51002, 10);
		GiveItem(51002, 10);
		GiveItem(51002, 10);
		GiveItem(51002, 10);
		GiveItem(51002, 10);
		GiveItem(51012, 10);
		GiveItem(51012, 10);
		GiveItem(51012, 10);
		GiveItem(51012, 10);
		GiveItem(60005, 10);
		GiveItem(60005, 10);
		GiveItem(60005, 10);
		GiveItem(63000, 10);
		GiveItem(91399);
		GiveItem(91399);
	}

	override protected void OnSkillsUpdate()
	{
		GiveSkill(SkillId.Rest, SkillRank.RD);
		GiveSkill(SkillId.Campfire, SkillRank.RE);
		GiveSkill(SkillId.Defense, SkillRank.R9);
		GiveSkill(SkillId.Smash, SkillRank.R9);
		GiveSkill(SkillId.Counterattack, SkillRank.RC);
		GiveSkill(SkillId.Windmill, SkillRank.RF);

		// ?
		// This is how the officials handle this
		GiveSkill(SkillId.CriticalHit, SkillRank.Novice);
		GiveSkill(SkillId.CombatMastery, SkillRank.R6);
		GiveSkill(SkillId.CriticalHit, SkillRank.R9);
	}

	override protected void OnPostUpdate()
	{
		// this.LifeMaxMod = 100.0f;
		this.AbilityPoints = 7;
		SetBaseStats(234, 50, 64, 224, 50);
		SetVitals(302.0f, 325.0f, 120.0f);
	}
}

public class RPMari : RolePlayingNPC
{
	public RPMari(string playerName)
	{
		NpcName = "Mari";
		BaseName = playerName;
	}

	override protected void OnPreUpdate()
	{
	}

	override protected void OnEquipmentUpdate()
	{
	}

	override protected void OnInventoryUpdate()
	{
		// TODO
	}

	override protected void OnSkillsUpdate()
	{
		// TODO
	}

	override protected void OnPostUpdate()
	{
		// TODO
	}
}