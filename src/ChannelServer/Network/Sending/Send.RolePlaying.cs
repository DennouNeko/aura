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
        public static void RequestSecondaryLogin(Creature creature, long EntityId)
        {
            Packet packet = new Packet(Op.RequestClientSecondaryLogin, MabiId.Channel);
            packet.PutLong(EntityId);
            packet.PutString(ChannelServer.Instance.Conf.Channel.ChannelHost);
            packet.PutShort((short)ChannelServer.Instance.Conf.Channel.ChannelPort);

            creature.Client.Send(packet);
            packet.Clear(packet.Op, packet.Id);
        }

        public static void RequestStartRP(Creature creature, long EntityId)
        {
            Packet packet = new Packet(Op.RequestClientStartRP, MabiId.Channel);
            packet.PutLong(EntityId);

            creature.Client.Send(packet);
            packet.Clear(packet.Op, packet.Id);
        }

        public static void RequestEndRP(Creature creature, long EntityId, int RegionId)
        {
            Packet packet = new Packet(Op.RequestClientEndRP, MabiId.Channel);
            packet.PutLong(EntityId);
            packet.PutInt(RegionId); // ?

            creature.Client.Send(packet);
            packet.Clear(packet.Op, packet.Id);
        }

		/// <summary>
		/// Some odd packet... Sending it with false and with true changes
		/// the appearance of "Character info" window and seems to blocks some
		/// of the menu buttons (they are still visible, though).
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="unk"></param>
		public static void UnknownRP(Creature creature, bool unk)
		{
			Packet temp = new Packet(0x90A4, creature.EntityId);
			temp.PutByte(unk);
			temp.PutByte(1);
			temp.PutInt(0);
			temp.PutInt(0);
			creature.Client.Send(temp);
		}
	}
}
