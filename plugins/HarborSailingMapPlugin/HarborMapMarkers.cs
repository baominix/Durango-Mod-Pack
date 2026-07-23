using System;
using System.Collections.Generic;
using Durango.Offline;
using Messages;
using Shared.System;
using OfflineConnection = Durango.Offline.Connection;
using OfflinePlayer = Durango.Offline.Player;
using PacketHeader = Durango.Network.PacketHeader;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    internal static class HarborMapMarkers
    {
        public static void Register(OfflinePlayer player, OfflineConnection connection, World world)
        {
            connection.Recv<GetExploredPOIs>(delegate(GetExploredPOIs msg, PacketHeader header)
            {
                player.Send<ExploredPOIs>(BuildExploredPorts(world), header.Seq);
            });

            connection.Recv<GetPOICount>(delegate(GetPOICount msg, PacketHeader header)
            {
                int count = CountPorts(world);
                player.Send<POICount>(new POICount
                {
                    PortCount = (byte)Math.Min(byte.MaxValue, count),
                    WarpholeCount = 0,
                    CraterCount = 0,
                    RiftCount = 0
                }, header.Seq);
            });

            connection.Recv<ExplorePOI>(delegate(ExplorePOI msg, PacketHeader header)
            {
                player.Send<OK>(default(OK), header.Seq);
            });
        }

        private static ExploredPOIs BuildExploredPorts(World world)
        {
            WorldContext context = HarborRuntime.GetWorldContext(world);
            List<Messages.PointOfInterest> ports = new List<Messages.PointOfInterest>();
            HashSet<Point2> occupiedTiles = new HashSet<Point2>();

            if (context != null && context.Artifacts != null)
            {
                foreach (AppearArtifact artifact in context.Artifacts.Values)
                {
                    if (artifact.EntityType != HarborSpawner.DockEntityType || !occupiedTiles.Add(artifact.Tile))
                    {
                        continue;
                    }

                    ports.Add(new Messages.PointOfInterest
                    {
                        Tile = artifact.Tile,
                        Type = Shared.System.PointOfInterest.Port,
                        Icon = "icon_map_port",
                        Title = "Harbor",
                        EntityType = HarborSpawner.DockEntityType,
                        IsExplored = true
                    });
                }
            }

            return new ExploredPOIs
            {
                POIs = ports.ToArray(),
                FullCountRewarded = false,
                RewardCost = null,
                IsOpenedMap = false
            };
        }

        private static int CountPorts(World world)
        {
            WorldContext context = HarborRuntime.GetWorldContext(world);
            if (context == null || context.Artifacts == null)
            {
                return 0;
            }

            HashSet<Point2> occupiedTiles = new HashSet<Point2>();
            foreach (AppearArtifact artifact in context.Artifacts.Values)
            {
                if (artifact.EntityType == HarborSpawner.DockEntityType)
                {
                    occupiedTiles.Add(artifact.Tile);
                }
            }
            return occupiedTiles.Count;
        }
    }
}
