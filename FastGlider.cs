using Chatting;
using MeshedObjects;
using Newtonsoft.Json.Linq;
using Pipliz;
using Pipliz.JSON;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Transport;
using UnityEngine;

namespace FastGlider
{
    [ModLoader.ModManager]
    public static class FastGlider
    {
        private static MeshedObjectType FastGliderType;
        static GliderSettings settings;
        static string MODPATH;

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "FastGlider.OnAssemblyLoaded")]
        public static void OnAssemblyLoaded(string path)
        {
            // Get a nicely formatted version of our mod directory.
            MODPATH = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "FastGlider.AfterAddingBaseTypes")]
        public static void afterAddingBaseTypes(Dictionary<string, ItemTypesServer.ItemTypeRaw> items)
        {
            // Create a node to store our block's data.
            JSONNode MyBlockJSON = new JSONNode();
            // Fill in some data.
            MyBlockJSON.SetAs("isPlaceable", false);
            MyBlockJSON.SetAs("icon", MODPATH + "/icons/glider.png");

            ItemTypesServer.ItemTypeRaw MyBlock = new ItemTypesServer.ItemTypeRaw(settings.ItemTypeName, MyBlockJSON);
            // Register
            items.Add("fastglider", MyBlock);
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterSelectedWorld, "FastGlider.glider_settings", -100f)]
        private static void Initialize()
        {
            settings = GliderSettings.Get();
            settings.MeshPath = "/meshes/fast_glider.ply";
            settings.MeshTypeKey = "fastglider";
            settings.ItemTypeName = "fastglider";

            
            // Just taking a quick look, this should be the default speed the mod uses. Change this to edit the users speed if they find the default to be too slow.
            SetSpeed(2);

            string str = MODPATH + settings.MeshPath;
            ServerManager.FileTable.StartLoading(str, ECachedFileType.Mesh);
            FastGlider.FastGliderType = MeshedObjectType.Register(new MeshedObjectTypeSettings(settings.MeshTypeKey, str, settings.TextureMapping)
            {
                colliders = settings.BoxColliders.Select<TransportManager.Box, RotatedBounds>((Func<TransportManager.Box, RotatedBounds>)(box => box.ToRotatedBounds)).ToList<RotatedBounds>(),
                InterpolationLooseness = 1.5f,
                sendUpdateRadius = 500
            });
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerClicked, "FastGlider.clicked_glider")]
        [ModLoader.ModCallbackProvidesFor("clicked_transport")]
        private static void OnClicked(Players.Player sender, PlayerClickedData data)
        {
            if (data.ConsumedType != PlayerClickedData.EConsumedType.Not || data.IsHoldingButton || (data.ClickType != PlayerClickedData.EClickType.Right || data.OnBuildCooldown) || (data.HitType != PlayerClickedData.EHitType.Block || (int)data.TypeSelected != (int)ItemTypes.GetType(settings.ItemTypeName).ItemIndex || !sender.Inventory.TryRemove(data.TypeSelected, 1, -1, true)))
                return;
            data.ConsumedType = PlayerClickedData.EConsumedType.UsedAsTool;
            FastGlider.CreateGlider(data.GetExactHitPositionWorld() + settings.SpawnOffset, Quaternion.identity, FastGlider.CreateVehicleDescription(MeshedObjectID.GetNew()), (Players.Player)null);
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnLoadWorldMisc, "FastGlider.load_gliders")]
        private static void LoadGliders(JObject rootObj)
        {
            JToken jtoken1;
            if (!rootObj.TryGetValue("transports", out jtoken1))
                return;
            if (jtoken1.Type != JTokenType.Array)
            {
                Log.WriteWarning("Didn't load gliders as transports wasn't an array");
            }
            else
            {
                JArray jarray = (JArray)jtoken1;
                int count = jarray.Count;
                for (int index = 0; index < count; ++index)
                {
                    JToken jtoken2 = jarray[index];
                    if (jtoken2.Type == JTokenType.Object)
                    {
                        JObject jobject = (JObject)jtoken2;
                        if (jobject.TryGetValue("type", out jtoken2) && jtoken2.Type == JTokenType.String)
                        {
                            if (!(jtoken2.Value<string>() != "fastglider"))
                            {
                                try
                                {
                                    Vector3 spawnPosition = ReadVector3((JObject)jobject["position"]);
                                    Quaternion rotation = Quaternion.Euler(ReadVector3((JObject)jobject["rotation"]));
                                    MeshedVehicleDescription vehicleDescription = FastGlider.CreateVehicleDescription(MeshedObjectID.GetNew());
                                    Players.Player player = (Players.Player)null;
                                    JToken jtoken3;
                                    if (jobject.TryGetValue("player", out jtoken3) && jtoken3.Type == JTokenType.String)
                                    {
                                        player = Players.GetPlayer(NetworkID.Parse((string)jtoken3));
                                        MeshedObjectManager.Attach(player, vehicleDescription);
                                    }
                                    FastGlider.CreateGlider(spawnPosition, rotation, vehicleDescription, player);
                                }
                                catch (Exception ex)
                                {
                                    Log.WriteException("Exception loading gliders:", ex);
                                }
                            }
                        }
                    }
                }
            }

            Vector3 ReadVector3(JObject obj)
            {
                return new Vector3(obj.Value<float>((object)"x"), obj.Value<float>((object)"y"), obj.Value<float>((object)"z"));
            }
        }

        public static MeshedVehicleDescription CreateVehicleDescription(MeshedObjectID ID)
        {
            return new MeshedVehicleDescription(new ClientMeshedObject(FastGlider.FastGliderType, ID), settings.PlayerOffset, settings.AllowPlayerEditingBlocks);
        }


        public static FastGlider.FastGliderTransport CreateGlider(
  Vector3 spawnPosition,
  Quaternion rotation,
  MeshedVehicleDescription vehicle,
  Players.Player playerInside)
        {
            Glider.GliderMovement mover = new Glider.GliderMovement(spawnPosition, rotation, settings, playerInside);
            FastGlider.FastGliderTransport vehicle1 = new FastGlider.FastGliderTransport(mover, vehicle, new InventoryItem(ItemTypes.GetType(settings.ItemTypeName).ItemIndex, 1));
            mover.SetParent(vehicle1);
            CollisionChecker.RegisterSource((CollisionChecker.ICollisionSource)mover);
            TransportManager.RegisterTransport((TransportManager.ITransportVehicle)vehicle1);
            return vehicle1;
        }


        public static void SetSpeed(int speed)
        {
            GliderSettings defaultSettings = GliderSettings.Get();
            settings.FlyUpMaxSpeed = defaultSettings.FlyUpMaxSpeed * speed;
            settings.FlyUpPower = defaultSettings.FlyUpPower * speed;
            settings.FlyForwardPowerMaxSpeed = defaultSettings.FlyForwardPowerMaxSpeed * speed;
            settings.FlyForwardPowerMax = defaultSettings.FlyForwardPowerMax * speed;
            settings.DragMaxSpeed = defaultSettings.DragMaxSpeed * speed;
        }

        public class FastGliderTransport : Glider.GliderTransport
        {
            public FastGliderTransport(
              Glider.GliderMovement mover,
              MeshedVehicleDescription description,
              InventoryItem refundItems)
              : base(mover, description, refundItems)
            {
            }

            public override JObject Save()
            {
                if (this.Mover == null)
                    return (JObject)null;
                Vector3 position = this.Mover.Position;
                Vector3 eulerAngles = this.Mover.Rotation.eulerAngles;
                JObject jobject = new JObject()
        {
          {
            "type",
            (JToken) "fastglider"
          },
          {
            "position",
            (JToken) new JObject()
            {
              {
                "x",
                (JToken) position.x
              },
              {
                "y",
                (JToken) position.y
              },
              {
                "z",
                (JToken) position.z
              }
            }
          },
          {
            "rotation",
            (JToken) new JObject()
            {
              {
                "x",
                (JToken) eulerAngles.x
              },
              {
                "y",
                (JToken) eulerAngles.y
              },
              {
                "z",
                (JToken) eulerAngles.z
              }
            }
          }
        };
                Glider.GliderMovement mover = this.Mover as Glider.GliderMovement;
                MeshedVehicleDescription description;
                if (mover.LastInputPlayer != null && MeshedObjectManager.TryGetVehicle(mover.LastInputPlayer, out description) && this.VehicleDescription.Object.ObjectID.ID == description.Object.ObjectID.ID)
                    jobject["player"] = (JToken)mover.LastInputPlayer.ID.ToString();
                return jobject;
            }

            public override void ProcessInputs(Players.Player player, Pipliz.Collections.SortedList<EInputKey, float> keyTimes, float deltaTime)
            {
                if ((double)(player.Position - this.Mover.Position).magnitude > 150.0)
                    MeshedObjectManager.Detach(player);
                else
                    this.Mover.ProcessInputs(player, keyTimes, deltaTime);
            }
        }


        [ChatCommandAutoLoader]
        public class Commands : IChatCommand
        {
            public bool TryDoCommand(Players.Player id, string chatItem, List<string> splits)
            {
                if (splits.Count >= 1 && splits[0] == "/speed")
                {
                    if (!PermissionsManager.CheckAndWarnPermission(id, (PermissionsManager.Permission)"FastGlider.speed"))
                    {
                        return true;
                    }

                    if (splits.Count == 1)
                    {
                        Chat.Send(id, "Set the speed using /speed [speed]");
                        return true;
                    }
                    else if (splits.Count == 2 && splits[0] == "/speed")
                    {
                        int result;
                        if (!int.TryParse(splits[1], out result))
                        {
                            Chat.Send(id, "Could not parse [" + splits[1] + "] as a number.", EChatSendOptions.Default);
                            return true;
                        }
                        if (result <= 0)
                        {
                            Chat.Send(id, "Speed cannot be 0 or lower. Use 2 to reset glider to default speed", EChatSendOptions.Default);
                            return true;
                        }
                        // FastGlider.SetSpeed(result); Remove the function being called to disable users setting their speed.
                        Chat.Send(id, "Appologies, Zero has disabled this chat command. Feel free to make suggestions on the default speed if you feel it's too low.");
                        return true;
                    }
                }
                return false;
            }
        }


    }
}
