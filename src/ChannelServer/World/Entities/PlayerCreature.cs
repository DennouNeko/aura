﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network;
using Aura.Channel.Network.Sending;
using Aura.Channel.World.Dungeons;
using Aura.Data;
using Aura.Mabi.Const;
using Aura.Mabi.Network;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aura.Channel.World.Entities
{
	/// <summary>
	/// Base class for creatures controlled by players.
	/// </summary>
	public abstract class PlayerCreature : Creature
	{
		private List<Entity> _visibleEntities = new List<Entity>();
		private object _lookAroundLock = new Object();

		/// <summary>
		/// Creature id, for creature database.
		/// </summary>
		public long CreatureId { get; set; }

		/// <summary>
		/// Server this creature exists on.
		/// </summary>
		public string Server { get; set; }

		/// <summary>
		/// Time at which the creature can be deleted.
		/// </summary>
		public DateTime DeletionTime { get; set; }

		/// <summary>
		/// Specifies whether to update visible creatures or not.
		/// </summary>
		public bool Watching { get; set; }

		/// <summary>
		/// Set to true if creature is supposed to be saved.
		/// </summary>
		public bool Save { get; set; }

		/// <summary>
		/// Player's CP, based on stats and skills.
		/// </summary>
		public override float CombatPower
		{
			get
			{
				var cp = 0f;

				cp += this.Skills.HighestSkillCp;
				cp += this.Skills.SecondHighestSkillCp * 0.5f;
				cp += this.LifeMaxBase;
				cp += this.ManaMaxBase * 0.5f;
				cp += this.StaminaMaxBase * 0.5f;
				cp += this.StrBase;
				cp += this.IntBase * 0.2f;
				cp += this.DexBase * 0.1f;
				cp += this.WillBase * 0.5f;
				cp += this.LuckBase * 0.1f;

				return cp;
			}
		}

		/// <summary>
		/// Creatures new PlayerCreature.
		/// </summary>
		public PlayerCreature()
		{
			this.Watching = true;
		}

		/// <summary>
		/// Instructs client to move to target location.
		/// Returns false if region doesn't exist.
		/// </summary>
		/// <param name="regionId"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public override bool Warp(int regionId, int x, int y)
		{
			if (regionId == this.RegionId && this.Region != Region.Limbo)
			{
				// The Tin cutscene at the very beginning requires a Warp to
				// fade away, otherwise the screen will stay black. However,
				// the cutscene in the first dungeon includes a jump to the
				// treasure room, where we can't warp, because that would drop
				// the treasure chest key. This is basically a hack until we
				// find a better way to properly get out of the Tin cutscene.
				if (this.Temp.CurrentCutscene == null || this.Region.IsDungeon)
				{
					this.Jump(x, y);
					return true;
				}
			}

			var targetRegion = ChannelServer.Instance.World.GetRegion(regionId);
			if (targetRegion == null)
			{
				Send.ServerMessage(this, "Warp failed, region doesn't exist.");
				Log.Error("PC.Warp: Region '{0}' doesn't exist.", regionId);
				return false;
			}

			var currentRegionId = this.RegionId;
			var loc = new Location(currentRegionId, this.GetPosition());

			this.LastLocation = loc;
			this.WarpLocation = new Location(regionId, x, y);
			this.Warping = true;
			this.Lock(Locks.Default, true);

			// TODO: We don't have to send the "create warps" every time,
			//   only when the player is warped there for the first time.

			// Dynamic Region warp
			var dynamicRegion = targetRegion as DynamicRegion;
			if (dynamicRegion != null)
			{
				if (!this.Region.IsTemp)
					this.FallbackLocation = loc;

				Send.EnterDynamicRegion(this, currentRegionId, targetRegion, x, y);

				return true;
			}

			// Dungeon warp
			var dungeonRegion = targetRegion as DungeonRegion;
			if (dungeonRegion != null)
			{
				if (!this.Region.IsTemp)
				{
					this.FallbackLocation = loc;
					this.DungeonSaveLocation = this.WarpLocation;
				}

				Send.DungeonInfo(this, dungeonRegion.Dungeon);

				return true;
			}

			// Normal warp
			Send.EnterRegion(this, regionId, x, y);

			return true;
		}

		/// <summary>
		/// Instructs client to login as another creature and warps it to specific location.
		/// Returns false if region doesn't exist.
		/// </summary>
		/// <param name="actor"></param>
		/// <param name="regionId"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public bool RequestSecondaryLogin(Creature actor, int regionId, int x, int y)
		{
			if (!(actor.Client is DummyClient))
				throw new ArgumentException(string.Format("{0} is already connected to another client!", actor.Name));

			Log.Debug("Logging in as {1} 0x{0:X16}", actor.EntityId, actor.Name);
			
			var targetRegion = ChannelServer.Instance.World.GetRegion(regionId);
			if (targetRegion == null)
			{
				Log.Error("RequestSecondaryLogin: Region '{0}' doesn't exist.", regionId);
				return false;
			}

			actor.SetLocation(regionId, x, y);
			// Seems like for all secondary logins it's the same, DefaultState | 0x10001000
			// RP NPC = 0x10001000
			// Golem  = 0xB0001004
			// Puppet = 0xB0001000
			actor.Activate(CreatureStates.InstantNpc); // 0x10000000
			actor.Activate(CreatureStates.EnableCommonPvp); // 0x00001000
			this.Client.Creatures.Add(actor.EntityId, actor);

			// Ask client to log in as a NPC and let it know it's a "pet"
			Send.RequestSecondaryLogin(this, actor.EntityId);
			Send.PetRegister(this, actor);

			return true;
		}
		
		/// <summary>
		/// Instructs client to switch to a RolePlaying character.
		/// </summary>
		/// <param name="actor"></param>
		/// <param name="hideBody"></param>
		public void StartRolePlaying(Creature actor, bool hideBody = true)
		{
			if(actor.Temp.RolePlayingController != null)
				throw new ArgumentException(string.Format("Role Playing Actor is already being used by {0}!", actor.Temp.RolePlayingController.Name));

			if (this.Temp.RolePlayingActor != null)
				throw new AccessViolationException(string.Format("{0} is already Role Playing as another character!", this.Name));

			actor.Temp.RolePlayingController = this;
			this.Temp.RolePlayingActor = actor;
			this.Temp.RolePlayingHidden = hideBody;

			if (hideBody)
			{
				this.Region.RemoveCreature(this);
			}

			var currentRegionId = this.RegionId;
			var loc = new Location(currentRegionId, this.GetPosition());

			this.LastLocation = loc;
			this.WarpLocation = loc;
			this.Warping = true;
			this.Lock(Locks.Default, true);

			this.Client.Controlling = actor;

			// Make the client switch to new character
			this.Lock(Locks.Default, true);
			Send.RequestStartRP(this, actor.EntityId);

			// Finalize the setup with warp
			Send.EnterRegion(this, currentRegionId, loc.X, loc.Y);

			Send.VehicleInfo(actor);
		}

		/// <summary>
		/// Instructs client to stop controlling a NPC.
		/// </summary>
		/// <remarks>
		/// By default it warps character back to its original location.
		/// To change it, call creature.SetLocation() before calling this.
		/// </remarks>
		public void EndRolePlaying()
		{
			var actor = this.Temp.RolePlayingActor as NPC;
			if (actor == null)
			{
				Send.ServerMessage(this, "Failed to disconect from NPC.");
				Log.Error("EndRolePlaying: RolePlayingActor is null or not a NPC");
				return;
			}

			if (actor.Client.NpcSession.IsValid())
				actor.Client.NpcSession.Clear();

			bool warpBack = this.Temp.IsRolePlayingInvisible;
			var loc = this.GetLocation();

			Send.StatUpdateDefault(this);

			var apos = actor.GetPosition();

			Send.RequestEndRP(this, loc.RegionId);

			this.Client.Controlling = this;
			this.Unlock(Locks.Default, true);

			TerminateSecondarySession(actor);

			this.Temp.RolePlayingActor = null;
			this.Temp.RolePlayingHidden = false;
			actor.Temp.RolePlayingController = null;

			if(warpBack)
			{
				var r = ChannelServer.Instance.World.GetRegion(loc.RegionId);
				r.AddCreature(this);
				this.Warp(loc);
			}
		}

		/// <summary>
		/// For disposing of creatures that were added by RequestSecondaryLogin.
		/// </summary>
		/// <param name="actor"></param>
		public void TerminateSecondarySession(Creature actor)
		{
			// TODO: put the creature on delayed removal list?
			// It seems that for any character, that player could control,
			// the end byte of EntityDisappears is 1
			// Known cases are: Golem, Puppet, RP NPC
			this.Client.Creatures.Remove(actor.EntityId);
			actor.Region.RemoveCreature(actor);

			actor.Dispose();
			Send.PetUnregister(this, actor);
			Send.Disappear(actor);
			actor.Client = new DummyClient();
			actor.Client.Kill();
		}

		/// <summary>
		/// Updates visible creatures, sends Entities(Dis)Appear.
		/// </summary>
		public void LookAround()
		{
			// To make sure we'll get a full update after Role Playing
			if(this.Temp.IsRolePlayingInvisible)
			{
				if (_visibleEntities.Count > 0)
				{
					Send.EntitiesDisappear(this.Client, _visibleEntities);
					_visibleEntities = new List<Entity>();
				}
				return;
			}

			if (!this.Watching)
				return;

			lock (_lookAroundLock)
			{
				var currentlyVisible = this.Region.GetVisibleEntities(this);

				var appear = currentlyVisible.Except(_visibleEntities);
				var disappear = _visibleEntities.Except(currentlyVisible);

				Send.EntitiesAppear(this.Client, appear);
				Send.EntitiesDisappear(this.Client, disappear);

				_visibleEntities = currentlyVisible;
			}
		}

		/// <summary>
		/// Returns whether player can target the given creature.
		/// </summary>
		/// <param name="creature"></param>
		/// <returns></returns>
		public override bool CanTarget(Creature creature)
		{
			if (!base.CanTarget(creature))
				return false;

			// Players can only target "bad" NPCs.
			if (creature.Has(CreatureStates.GoodNpc))
				return false;

			// Players can't target players (outside of PvP, TODO)
			if (creature.IsPlayer)
				return false;

			return true;
		}

		/// <summary>
		/// Players survive when they had more than half of their life left.
		/// </summary>
		/// <param name="damage"></param>
		/// <param name="from"></param>
		/// <param name="lifeBefore"></param>
		/// <returns></returns>
		protected override bool ShouldSurvive(float damage, Creature from, float lifeBefore)
		{
			return (lifeBefore >= this.LifeMax / 2);
		}

		/// <summary>
		/// Aggroes target, setting target and putting creature in battle stance.
		/// </summary>
		/// <param name="creature"></param>
		public override void Aggro(Creature target)
		{
			this.IsInBattleStance = true;
		}
	}
}
