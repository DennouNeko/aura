﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;
using System.Collections.Generic;
using System.Linq;
using Aura.Channel.Network;
using Aura.Channel.Network.Sending;
using Aura.Channel.Util;
using Aura.Channel.World.Entities;
using Aura.Data;
using Aura.Mabi.Const;
using Aura.Shared.Network;
using Aura.Shared.Util;
using System.Threading;
using Aura.Data.Database;
using Boo.Lang.Compiler.TypeSystem;
using System.Drawing;
using Aura.Channel.Scripting.Scripts;
using Aura.Mabi.Network;

namespace Aura.Channel.World
{
	public class Region
	{
		// TODO: Data?
		public const int VisibleRange = 3000;

		protected ReaderWriterLockSlim _creaturesRWLS, _propsRWLS, _itemsRWLS;

		/// <summary>
		/// List of areas in this region
		/// </summary>
		/// <remarks>
		/// The reason for this list is that we need a region specific list of
		/// areas, because dynamic regions change the area's ids. We can't use
		/// the original area information to identify them.
		/// </remarks>
		protected List<AreaData> _areas;

		/// <summary>
		/// List of client events in this region
		/// </summary>
		/// <remarks>
		/// The reason for this list is that we need a region specific list of
		/// events, because dynamic regions change the area's ids. We can't use
		/// the original area information to identify them.
		/// </remarks>
		protected Dictionary<long, EventData> _events;

		protected Dictionary<long, Creature> _creatures;
		protected Dictionary<long, Prop> _props;
		protected Dictionary<long, Item> _items;

		protected HashSet<ChannelClient> _clients;

		public RegionInfoData RegionInfoData { get; protected set; }

		/// <summary>
		/// Region's name
		/// </summary>
		public string Name { get; protected set; }

		/// <summary>
		/// Name of the region this one is based on (dynamics)
		/// </summary>
		public string BaseName { get; protected set; }

		/// <summary>
		/// Region's id
		/// </summary>
		public int Id { get; protected set; }

		/// <summary>
		/// Id of the region this one is based on (dynamics)
		/// </summary>
		public int BaseId { get; protected set; }

		/// <summary>
		/// Variation file used for this region (dynamics)
		/// </summary>
		public string Variation { get; protected set; }

		/// <summary>
		/// Returns true if this is a dynamic region.
		/// </summary>
		public bool IsDynamic { get { return this.Id != this.BaseId; } }

		/// <summary>
		/// Manager for blocking objects in the region.
		/// </summary>
		public RegionCollision Collisions { get; protected set; }

		/// <summary>
		/// Creates new region by id.
		/// </summary>
		/// <param name="regionId"></param>
		private Region(int regionId)
		{
			_creaturesRWLS = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
			_propsRWLS = new ReaderWriterLockSlim();
			_itemsRWLS = new ReaderWriterLockSlim();

			this.Id = regionId;
			this.BaseId = regionId;

			_areas = new List<AreaData>();

			_events = new Dictionary<long, EventData>();

			_creatures = new Dictionary<long, Creature>();
			_props = new Dictionary<long, Prop>();
			_items = new Dictionary<long, Item>();

			_clients = new HashSet<ChannelClient>();

			this.Collisions = new RegionCollision();

			this.RegionInfoData = AuraData.RegionInfoDb.Find(this.Id);
			if (this.RegionInfoData == null)
			{
				Log.Warning("Region: No data found for '{0}'.", this.Id);
				return;
			}
		}

		/// <summary>
		/// Creates new region by id.
		/// </summary>
		/// <param name="regionId"></param>
		public static Region CreateNormal(int regionId)
		{
			var region = new Region(regionId);

			region.InitializeFromData();

			return region;
		}

		/// <summary>
		/// Creates new dynamic region, based on regionId and variant.
		/// Region is automatically added to the dynamic region manager.
		/// </summary>
		/// <param name="regionId"></param>
		/// <param name="variation"></param>
		public static Region CreateDynamic(int baseRegionId, string variation)
		{
			var region = new Region(baseRegionId);
			region.Id = ChannelServer.Instance.World.DynamicRegions.GetFreeDynamicRegionId();
			region.Variation = variation;

			region.InitializeFromData();

			ChannelServer.Instance.World.DynamicRegions.Add(region);

			return region;
		}

		/// <summary>
		/// Adds all props found in the client for this region and creates a list
		/// of areas.
		/// </summary>
		protected void InitializeFromData()
		{
			if (this.RegionInfoData == null || this.RegionInfoData.Areas == null)
				return;

			var regionData = AuraData.RegionDb.Find(this.BaseId);
			if (regionData != null)
				this.BaseName = regionData.Name;

			this.Name = (this.IsDynamic ? "Dynamic" + this.Id : this.BaseName);

			this.Collisions.Init(this.RegionInfoData);

			this.LoadAreas();
			this.LoadProps();
			this.LoadEvents();
		}

		/// <summary>
		/// Creates a list of all areas.
		/// </summary>
		protected void LoadAreas()
		{
			foreach (var area in this.RegionInfoData.Areas)
			{
				var newArea = area.Copy();
				lock (_areas)
					_areas.Add(newArea);
			}
		}

		/// <summary>
		/// Adds all props found in the client for this region.
		/// </summary>
		protected void LoadProps()
		{
			foreach (var area in _areas)
			{
				foreach (var prop in area.Props.Values)
				{
					// Use the id given by the client data as base, but replace
					// region and area ids in case of this being a dynamic region.
					var entityId = (ulong)prop.EntityId;
					entityId &= ~0x0000FFFFFFFF0000U;
					entityId |= ((ulong)this.Id << 32);
					entityId |= ((ulong)this.GetAreaId(prop.EntityId) << 16);

					var add = new Prop((long)entityId, "", "", prop.Id, this.Id, (int)prop.X, (int)prop.Y, prop.Direction, prop.Scale, 0);

					// Add drop behaviour if drop type exists
					var dropType = prop.GetDropType();
					if (dropType != -1) add.Behavior = Prop.GetDropBehavior(dropType);

					this.AddProp(add);
				}
			}
		}

		/// <summary>
		/// Adds all events found in the client for this region.
		/// </summary>
		protected void LoadEvents()
		{
			foreach (var area in _areas)
			{
				foreach (var ev in area.Events.Values)
				{
					// Use the id given by the client data as base, but replace
					// region and area ids in case of this being a dynamic region.
					var eventId = (ulong)ev.Id;
					eventId &= ~0x0000FFFFFFFF0000U;
					eventId |= ((ulong)this.Id << 32);
					eventId |= ((ulong)this.GetAreaId(ev.Id) << 16);

					var newEvent = ev.Copy();
					newEvent.Id = (long)eventId;
					newEvent.RegionId = this.Id;

					lock (_events)
						_events.Add(newEvent.Id, newEvent);
				}
			}
		}

		/// <summary>
		/// Returns event by id or null if it doesn't exist.
		/// </summary>
		/// <param name="eventId"></param>
		/// <returns></returns>
		public EventData GetEvent(long eventId)
		{
			if (!_events.ContainsKey(eventId))
				return null;

			return _events[eventId];
		}

		/// <summary>
		/// Extracts area id from prop entity id and adjusts it if region is dynamic.
		/// </summary>
		/// <param name="entityId"></param>
		/// <returns></returns>
		private int GetAreaId(long entityId)
		{
			var areaId = (int)(((ulong)entityId & ~0xFFFFFFFF0000FFFF) >> 16);

			areaId = this.AdjustAreaIdForDynamics(areaId);

			return areaId;
		}

		/// <summary>
		/// Returns id of area at the given coordinates and adjusts it if region is dynamic, or 0 if area wasn't found.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public int GetAreaId(int x, int y)
		{
			var areaId = 0;

			foreach (var area in _areas)
			{
				if (x >= Math.Min(area.X1, area.X2) && x < Math.Max(area.X1, area.X2) && y >= Math.Min(area.Y1, area.Y2) && y < Math.Max(area.Y1, area.Y2))
					areaId = area.Id;
			}

			areaId = this.AdjustAreaIdForDynamics(areaId);

			return areaId;
		}

		/// <summary>
		/// Changes area id, if necessary, for dynamic regions.
		/// </summary>
		/// <remarks>
		/// Areas in the client files aren't in order, it can be id 1, 2, 3,
		/// but it can just as well be 2, 1, 3. When the client creates a dynamic
		/// region it changes the ids to be order, so 2, 1, 3 would become
		/// 1, 2, 3 in the dynamic version. We have to mimic this to get the
		/// correct ids for the props. Genius system, thanks, devCAT.
		/// 
		/// What we do here is returning the index of the area in the list.
		/// </remarks>
		/// <param name="areaId"></param>
		/// <returns></returns>
		private int AdjustAreaIdForDynamics(int areaId)
		{
			if (!this.IsDynamic)
				return areaId;

			var id = 1;
			foreach (var area in _areas)
			{
				if (area.Id == areaId)
					return id;

				id++;
			}

			throw new Exception("Area '" + areaId + "' not found in '" + this.BaseId + "'.");
		}

		/// <summary>
		/// Updates all entites, removing dead ones, updating visibility, etc.
		/// </summary>
		public void UpdateEntities()
		{
			this.RemoveOverdueEntities();
			this.UpdateVisibility();
		}

		/// <summary>
		/// Removes expired entities. 
		/// </summary>
		private void RemoveOverdueEntities()
		{
			var now = DateTime.Now;

			// Get all expired entities
			var disappear = new List<Entity>();

			_creaturesRWLS.EnterReadLock();
			try
			{
				disappear.AddRange(_creatures.Values.Where(a => a.DisappearTime > DateTime.MinValue && a.DisappearTime < now));
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}

			_itemsRWLS.EnterReadLock();
			try
			{
				disappear.AddRange(_items.Values.Where(a => a.DisappearTime > DateTime.MinValue && a.DisappearTime < now));
			}
			finally
			{
				_itemsRWLS.ExitReadLock();
			}

			_propsRWLS.EnterReadLock();
			try
			{
				disappear.AddRange(_props.Values.Where(a => a.DisappearTime > DateTime.MinValue && a.DisappearTime < now));
			}
			finally
			{
				_propsRWLS.ExitReadLock();
			}

			// Remove them from the region
			foreach (var entity in disappear)
			{
				if (entity.Is(DataType.Creature))
				{
					var creature = entity as Creature;
					this.RemoveCreature(creature);
					creature.Dispose();

					// Respawn
					var npc = creature as NPC;
					if (npc != null && npc.SpawnId > 0)
						ChannelServer.Instance.ScriptManager.Spawn(npc.SpawnId, 1);
				}
				else if (entity.Is(DataType.Item))
				{
					this.RemoveItem(entity as Item);
				}
				else if (entity.Is(DataType.Prop))
				{
					this.RemoveProp(entity as Prop);
				}
			}
		}

		/// <summary>
		/// Updates visible entities on all clients.
		/// </summary>
		private void UpdateVisibility()
		{
			_creaturesRWLS.EnterReadLock();
			try
			{
				foreach (var creature in _creatures.Values)
				{
					var pc = creature as PlayerCreature;

					// Only update player creatures
					if (pc == null)
						continue;

					pc.LookAround();
				}
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}
		}

		/// <summary>
		/// Returns a list of visible entities, from the view point of creature.
		/// </summary>
		/// <param name="creature"></param>
		public List<Entity> GetVisibleEntities(Creature creature)
		{
			var result = new List<Entity>();
			var pos = creature.GetPosition();

			// Players don't see anything else while they're watching a cutscene.
			// This automatically (de)spawns entities (from LookAround) while watching.
			if (creature.Temp.CurrentCutscene == null || !creature.IsPlayer)
			{
				_creaturesRWLS.EnterReadLock();
				try
				{
					result.AddRange(_creatures.Values.Where(a => a.GetPosition().InRange(pos, VisibleRange) && !a.Conditions.Has(ConditionsA.Invisible)));
				}
				finally
				{
					_creaturesRWLS.ExitReadLock();
				}

				_itemsRWLS.EnterReadLock();
				try
				{
					result.AddRange(_items.Values.Where(a => a.GetPosition().InRange(pos, VisibleRange)));
				}
				finally
				{
					_itemsRWLS.ExitReadLock();
				}
			}

			_propsRWLS.EnterReadLock();
			try
			{
				// Send all props of a region, so they're visible from afar.
				// While client props are visible as well they don't have to
				// be sent, the client already has them.
				//
				// ^^^^^^^^^^^^^^^^^^ This caused a bug with client prop states
				// not being set until the prop was used by a player while
				// the creature was in the region (eg windmill) so we'll count
				// all props as visible. -- Xcelled
				//
				// ^^^^^^^^^^^^^^^^^^ That causes a huge EntitiesAppear packet,
				// because there are thousands of client props. We only need
				// the ones that make a difference. Added check for
				// state and XML. [exec]

				result.AddRange(_props.Values.Where(a => a.ServerSide || a.ModifiedClientSide));
			}
			finally
			{
				_propsRWLS.ExitReadLock();
			}

			return result;
		}

		/// <summary>
		/// Adds creature to region, sends EntityAppears.
		/// </summary>
		public void AddCreature(Creature creature)
		{
			if (creature.Region != null)
				creature.Region.RemoveCreature(creature);

			_creaturesRWLS.EnterWriteLock();
			try
			{
				_creatures.Add(creature.EntityId, creature);
			}
			finally
			{
				_creaturesRWLS.ExitWriteLock();
			}

			creature.Region = this;

			// Save reference to client if it's mainly controlling this creature.
			if (creature.Client.Controlling == creature)
			{
				lock (_clients)
					_clients.Add(creature.Client);
			}

			// TODO: Technically not required? Handled by LookAround.
			Send.EntityAppears(creature);

			if (creature.EntityId < MabiId.Npcs)
				Log.Status("Creatures currently in region {0}: {1}", this.Id, _creatures.Count);
		}

		/// <summary>
		/// Removes creature from region, sends EntityDisappears.
		/// </summary>
		public void RemoveCreature(Creature creature)
		{
			_creaturesRWLS.EnterWriteLock();
			try
			{
				_creatures.Remove(creature.EntityId);
			}
			finally
			{
				_creaturesRWLS.ExitWriteLock();
			}

			// TODO: Technically not required? Handled by LookAround.
			Send.EntityDisappears(creature);

			creature.Region = null;

			if (creature.Client.Controlling == creature)
				lock (_clients)
					_clients.Remove(creature.Client);

			if (creature.EntityId < MabiId.Npcs)
				Log.Status("Creatures currently in region {0}: {1}", this.Id, _creatures.Count);
		}

		/// <summary>
		/// Returns creature by entityId, or null, if it doesn't exist.
		/// </summary>
		public Creature GetCreature(long entityId)
		{
			Creature creature;

			_creaturesRWLS.EnterReadLock();
			try
			{
				_creatures.TryGetValue(entityId, out creature);
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}

			return creature;
		}

		/// <summary>
		/// Returns creature by name, or null, if it doesn't exist.
		/// </summary>
		public Creature GetCreature(string name)
		{
			_creaturesRWLS.EnterReadLock();
			try
			{
				return _creatures.Values.FirstOrDefault(a => a.Name == name);
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}
		}

		/// <summary>
		/// Returns NPC by entityId, or null, if no NPC with that id exists.
		/// </summary>
		public NPC GetNpc(long entityId)
		{
			return this.GetCreature(entityId) as NPC;
		}

		/// <summary>
		/// Returns NPC by entity id, throws SevereViolation exception if
		/// NPC doesn't exist.
		/// </summary>
		/// <param name="entityId"></param>
		/// <returns></returns>
		public NPC GetNpcSafe(long entityId)
		{
			var npc = this.GetNpc(entityId);

			if (npc == null)
				throw new SevereViolation("Tried to get a nonexistant NPC");

			return npc;
		}

		/// <summary>
		/// Returns creature by entity id, throws SevereViolation exception if
		/// creature doesn't exist.
		/// </summary>
		/// <param name="entityId"></param>
		/// <returns></returns>
		public Creature GetCreatureSafe(long entityId)
		{
			var creature = this.GetCreature(entityId);

			if (creature == null)
				throw new SevereViolation("Tried to get a nonexistant creature");

			return creature;
		}

		/// <summary>
		/// Returns first player creature with the given name, or null.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public PlayerCreature GetPlayer(string name)
		{
			_creaturesRWLS.EnterReadLock();
			try
			{
				return _creatures.Values.FirstOrDefault(a => a is PlayerCreature && a.Name == name) as PlayerCreature;
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}
		}

		/// <summary>
		/// Returns all player creatures in range.
		/// </summary>
		/// <param name="pos"></param>
		/// <param name="range"></param>
		/// <returns></returns>
		public List<Creature> GetPlayersInRange(Position pos, int range = VisibleRange)
		{
			_creaturesRWLS.EnterReadLock();
			try
			{
				return _creatures.Values.Where(a => a.IsPlayer && a.GetPosition().InRange(pos, range)).ToList();
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}
		}

		/// <summary>
		/// Returns all player creatures in region.
		/// </summary>
		/// <returns></returns>
		public List<Creature> GetAllPlayers()
		{
			_creaturesRWLS.EnterReadLock();
			try
			{
				return _creatures.Values.Where(a => a.IsPlayer).ToList();
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}
		}

		/// <summary>
		/// Returns amount of players in region.
		/// </summary>
		/// <returns></returns>
		public int CountPlayers()
		{
			_creaturesRWLS.EnterReadLock();
			try
			{
				// Count any player creatures that are directly controlled,
				// filtering creatures with masters (pets/partners).
				return _creatures.Values.Count(a => a is PlayerCreature && a.Master == null);
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}
		}

		/// <summary>
		/// Returns all visible creatures in range of entity, excluding itself.
		/// </summary>
		/// <param name="entity"></param>
		/// <param name="range"></param>
		/// <returns></returns>
		public List<Creature> GetVisibleCreaturesInRange(Entity entity, int range = VisibleRange)
		{
			_creaturesRWLS.EnterReadLock();
			try
			{
				return _creatures.Values.Where(a => a != entity && a.GetPosition().InRange(entity.GetPosition(), range) && !a.Conditions.Has(ConditionsA.Invisible)).ToList();
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}
		}

		/// <summary>
		///  Spawns prop, sends EntityAppears.
		/// </summary>
		public void AddProp(Prop prop)
		{
			_propsRWLS.EnterWriteLock();
			try
			{
				_props.Add(prop.EntityId, prop);
			}
			finally
			{
				_propsRWLS.ExitWriteLock();
			}

			prop.Region = this;

			Send.EntityAppears(prop);
		}

		/// <summary>
		/// Despawns prop, sends EntityDisappears.
		/// </summary>
		public void RemoveProp(Prop prop)
		{
			if (!prop.ServerSide)
			{
				Log.Error("RemoveProp: Client side props can't be removed.");
				prop.DisappearTime = DateTime.MinValue;
				return;
			}

			_propsRWLS.EnterWriteLock();
			try
			{
				_props.Remove(prop.EntityId);
			}
			finally
			{
				_propsRWLS.ExitWriteLock();
			}

			Send.PropDisappears(prop);

			prop.Region = null;
		}

		/// <summary>
		/// Returns prop or null.
		/// </summary>
		public Prop GetProp(long entityId)
		{
			Prop result;

			_propsRWLS.EnterReadLock();
			try
			{
				_props.TryGetValue(entityId, out result);
			}
			finally
			{
				_propsRWLS.ExitReadLock();
			}

			return result;
		}

		/// <summary>
		///  Adds item, sends EntityAppears.
		/// </summary>
		public void AddItem(Item item)
		{
			_itemsRWLS.EnterWriteLock();
			try
			{
				_items.Add(item.EntityId, item);
			}
			finally
			{
				_itemsRWLS.ExitWriteLock();
			}

			item.Region = this;

			Send.EntityAppears(item);
		}

		/// <summary>
		/// Despawns item, sends EntityDisappears.
		/// </summary>
		public void RemoveItem(Item item)
		{
			_itemsRWLS.EnterWriteLock();
			try
			{
				_items.Remove(item.EntityId);
			}
			finally
			{
				_itemsRWLS.ExitWriteLock();
			}

			Send.EntityDisappears(item);

			item.Region = null;
		}

		/// <summary>
		/// Returns item or null.
		/// </summary>
		public Item GetItem(long entityId)
		{
			Item result;

			_itemsRWLS.EnterReadLock();
			try
			{
				_items.TryGetValue(entityId, out result);
			}
			finally
			{
				_itemsRWLS.ExitReadLock();
			}

			return result;
		}

		/// <summary>
		/// Returns a list of all items on the floor.
		/// </summary>
		public List<Item> GetAllItems()
		{
			List<Item> result;

			_itemsRWLS.EnterReadLock();
			try
			{
				result = new List<Item>(_items.Values);
			}
			finally
			{
				_itemsRWLS.ExitReadLock();
			}

			return result;
		}

		/// <summary>
		/// Drops item into region and makes it disappear after x seconds.
		/// Sends EntityAppears.
		/// </summary>
		public void DropItem(Item item, int x, int y)
		{
			item.Move(this.Id, x, y);
			item.DisappearTime = DateTime.Now.AddSeconds(Math.Max(60, (item.OptionInfo.Price / 100) * 60));

			this.AddItem(item);
		}

		/// <summary>
		/// Returns new list of all entities within range of source.
		/// </summary>
		public List<Entity> GetEntitiesInRange(Entity source, int range = -1)
		{
			if (range < 0)
				range = VisibleRange;

			var result = new List<Entity>();

			_creaturesRWLS.EnterReadLock();
			try
			{
				result.AddRange(_creatures.Values.Where(a => a.GetPosition().InRange(source.GetPosition(), range)));
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}

			_itemsRWLS.EnterReadLock();
			try
			{
				result.AddRange(_items.Values.Where(a => a.GetPosition().InRange(source.GetPosition(), range)));
			}
			finally
			{
				_itemsRWLS.ExitReadLock();
			}

			_propsRWLS.EnterReadLock();
			try
			{
				// All props are visible, but not all of them are in range.
				result.AddRange(_props.Values.Where(a => a.GetPosition().InRange(source.GetPosition(), range)));
			}
			finally
			{
				_propsRWLS.ExitReadLock();
			}

			return result;
		}

		/// <summary>
		/// Returns new list of all creatures within range of position.
		/// </summary>
		public List<Creature> GetCreaturesInRange(Position pos, int range)
		{
			var result = new List<Creature>();

			_creaturesRWLS.EnterReadLock();
			try
			{
				result.AddRange(_creatures.Values.Where(a => a.GetPosition().InRange(pos, range)));
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}

			return result;
		}

		/// <summary>
		/// Returns new list of all creatures within the specified polygon.
		/// </summary>
		public List<Creature> GetCreaturesInPolygon(params Point[] points)
		{
			var result = new List<Creature>();

			_creaturesRWLS.EnterReadLock();
			try
			{
				result.AddRange(_creatures.Values.Where(a => a.GetPosition().InPolygon(points)));
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}

			return result;
		}

		/// <summary>
		/// Removes all scripted entites from this region.
		/// </summary>
		public void RemoveScriptedEntities()
		{
			// Get NPCs
			var npcs = new List<Creature>();
			_creaturesRWLS.EnterReadLock();
			try { npcs.AddRange(_creatures.Values.Where(a => a is NPC)); }
			finally { _creaturesRWLS.ExitReadLock(); }

			// Get server side props
			var props = new List<Prop>();
			_propsRWLS.EnterReadLock();
			try { props.AddRange(_props.Values.Where(a => a.ServerSide)); }
			finally { _propsRWLS.ExitReadLock(); }

			// Remove all
			foreach (var npc in npcs) { this.RemoveCreature(npc); npc.Dispose(); }
			foreach (var prop in props) this.RemoveProp(prop);
		}

		/// <summary>
		/// Activates AIs in range of the movement path.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="from"></param>
		/// <param name="to"></param>
		public void ActivateAis(Creature creature, Position from, Position to)
		{
			// Bounding rectangle
			var minX = Math.Min(from.X, to.X) - VisibleRange;
			var minY = Math.Min(from.Y, to.Y) - VisibleRange;
			var maxX = Math.Max(from.X, to.X) + VisibleRange;
			var maxY = Math.Max(from.Y, to.Y) + VisibleRange;

			// Activation
			_creaturesRWLS.EnterReadLock();
			try
			{
				foreach (var npc in _creatures.Values.OfType<NPC>())
				{
					if (npc.AI == null)
						continue;

					var pos = npc.GetPosition();
					if (!(pos.X >= minX && pos.X <= maxX && pos.Y >= minY && pos.Y <= maxY))
						continue;

					var time = (from.GetDistance(to) / creature.GetSpeed()) * 1000;

					npc.AI.Activate(time);
				}
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}
		}

		/// <summary>
		/// Returns amount of creatures of race that are targetting target
		/// in this region.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="raceId"></param>
		/// <returns></returns>
		public int CountAggro(Creature target, int raceId)
		{
			_creaturesRWLS.EnterReadLock();
			try
			{
				return _creatures.Values.OfType<NPC>().Count(npc =>
					!npc.IsDead &&
					npc.AI != null &&
					npc.AI.State == AiScript.AiState.Aggro &&
					npc.Race == raceId &&
					npc.Target == target
				);
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}
		}

		/// <summary>
		/// Returns amount of creatures of race that are targetting target
		/// in this region.
		/// </summary>
		/// <param name="target"></param>
		/// <returns></returns>
		public int CountAggro(Creature target)
		{
			_creaturesRWLS.EnterReadLock();
			try
			{
				return _creatures.Values.OfType<NPC>().Count(npc =>
					!npc.IsDead &&
					npc.AI != null &&
					npc.AI.State == AiScript.AiState.Aggro &&
					npc.Target == target
				);
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}
		}

		/// <summary>
		/// Adds all good NPCs of region to list.
		/// </summary>
		/// <param name="list"></param>
		public void GetAllGoodNpcs(ref List<Creature> list)
		{
			_creaturesRWLS.EnterReadLock();
			try
			{
				list.AddRange(_creatures.Values.Where(a => a.Has(CreatureStates.GoodNpc) && a is NPC));
			}
			finally
			{
				_creaturesRWLS.ExitReadLock();
			}
		}

		/// <summary>
		/// Broadcasts packet in region.
		/// </summary>
		public void Broadcast(Packet packet)
		{
			lock (_clients)
			{
				foreach (var client in _clients)
					client.Send(packet);
			}
		}

		/// <summary>
		/// Broadcasts packet to all creatures in range of source.
		/// </summary>
		public void Broadcast(Packet packet, Entity source, bool sendToSource = true, int range = -1)
		{
			if (range < 0)
				range = VisibleRange;

			var pos = source.GetPosition();

			lock (_clients)
			{
				foreach (var client in _clients)
				{
					if (!client.Controlling.GetPosition().InRange(pos, range))
						continue;

					if (client.Controlling == source && !sendToSource)
						continue;

					client.Send(packet);
				}
			}
		}
	}
}
