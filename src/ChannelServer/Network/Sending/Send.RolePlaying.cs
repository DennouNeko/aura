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
    }
}
