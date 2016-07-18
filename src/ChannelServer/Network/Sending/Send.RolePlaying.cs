// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.World.Entities;
using Aura.Mabi.Const;
using Aura.Mabi.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.Channel.Network.Sending
{
	public static partial class Send
	{
		/// <summary>
		/// Sends a request to client, to start secondary session
		/// using existing connection.
		/// </summary>
		/// <param name="creature">"master"</param>
		/// <param name="EntityId">secondary creature's EntityId</param>
		public static void RequestSecondaryLogin(Creature creature, long EntityId)
		{
			Packet packet = new Packet(Op.RequestClientSecondaryLogin, MabiId.Channel);
			packet.PutLong(EntityId);
			packet.PutString(ChannelServer.Instance.Conf.Channel.ChannelHost);
			packet.PutShort((short)ChannelServer.Instance.Conf.Channel.ChannelPort);

			creature.Client.Send(packet);
			packet.Clear(packet.Op, packet.Id);
		}

		/// <summary>
		/// Sends a request to client, to switch to a Role Playing character
		/// </summary>
		/// <param name="creature">"master"</param>
		/// <param name="EntityId">secondary creature's EntityId</param>
		public static void RequestStartRP(Creature creature, long EntityId)
		{
			Packet packet = new Packet(Op.RequestClientStartRP, MabiId.Channel);
			packet.PutLong(EntityId);

			creature.Client.Send(packet);
			packet.Clear(packet.Op, packet.Id);
		}

		/// <summary>
		/// Requests client to stop Role Playing and switch back to main
		/// character.
		/// </summary>
		/// <param name="creature">"master" to return to</param>
		/// <param name="RegionId">Seems to be the return region</param>
		public static void RequestEndRP(Creature creature, int RegionId)
		{
			Packet packet = new Packet(Op.RequestClientEndRP, MabiId.Channel);
			packet.PutLong(creature.EntityId);
			packet.PutInt(RegionId); // ?

			creature.Client.Send(packet);
			packet.Clear(packet.Op, packet.Id);
		}
	}
}
