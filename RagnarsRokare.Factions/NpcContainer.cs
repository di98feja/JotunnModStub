using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal class NpcContainer : Container, Interactable
    {
        private Humanoid m_npc;
        private Inventory m_humanoidInventory;
        private static bool m_isRightClick = false;

        private new void Awake()
        {
            base.Awake();
            m_nview.Unregister("OpenRespons");
            m_nview.Register<bool>("OpenRespons", RPC_OpenRespons);

            m_npc = gameObject.GetComponent<Humanoid>();
            On.InventoryGui.OnRightClickItem += InventoryGui_OnRightClickItem;
            On.Humanoid.EquipItem += Humanoid_EquipItem;
            On.Humanoid.UnequipItem += Humanoid_UnequipItem;
            On.Humanoid.GiveDefaultItems += Humanoid_GiveDefaultItems;
        }

        private void Humanoid_GiveDefaultItems(On.Humanoid.orig_GiveDefaultItems orig, Humanoid self)
        {
            if (self.gameObject.name.Contains("NPC"))
            {
                m_isRightClick = true;
            }
            orig(self);
            m_isRightClick = false;
        }

        private new void RPC_OpenRespons(long uid, bool granted)
        {
            if ((bool)Player.m_localPlayer)
            {
                if (granted)
                {
                    m_inventory = m_humanoidInventory;
                    var npcZdo = gameObject.GetComponent<ZNetView>().GetZDO();
                    m_name = npcZdo.GetString(Constants.Z_GivenName);

                    InventoryGui.instance.Show(this);
                }
                else
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_inuse");
                }
            }
        }

        private bool Humanoid_EquipItem(On.Humanoid.orig_EquipItem orig, Humanoid self, ItemDrop.ItemData item, bool triggerEquipEffects)
        {
            if (m_isRightClick || !self.gameObject.name.Contains("NPC"))
            {
                orig(self, item, triggerEquipEffects);
            }
            else
            {
                HoldRightHandItem(self, item);
            }
            return true;
        }

        private void Humanoid_UnequipItem(On.Humanoid.orig_UnequipItem orig, Humanoid self, ItemDrop.ItemData item, bool triggerEquipEffects)
        {
            if (m_isRightClick || !self.gameObject.name.Contains("NPC"))
            {
                orig(self, item, triggerEquipEffects);
            }
            else
            {
                HoldRightHandItem(self, null);
            }
        }


        private void HoldRightHandItem(Humanoid self, ItemDrop.ItemData item)
        {
            if (self.m_rightItem == item) return;

            self.m_rightItem = item;

            var itemHash = item?.m_dropPrefab.name.GetStableHashCode() ?? 0;
            self.m_visEquipment.SetRightItem(item?.m_dropPrefab.name ?? "");
            self.m_visEquipment.m_currentRightItemHash = itemHash;
            if ((bool)self.m_visEquipment.m_rightItemInstance)
            {
                UnityEngine.Object.Destroy(self.m_visEquipment.m_rightItemInstance);
                self.m_visEquipment.m_rightItemInstance = null;
            }
            if (itemHash != 0)
            {
                GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(itemHash);
                if (itemPrefab == null)
                {
                    Jotunn.Logger.LogDebug("Missing attach item: " + itemHash + "  ob:" + base.gameObject.name);
                    return;
                }

                GameObject gameObject = itemPrefab.transform.childCount > 0 ? itemPrefab.transform.GetChild(0).gameObject : null;
                if (gameObject != null)
                {
                    GameObject itemInstance = UnityEngine.Object.Instantiate(gameObject);
                    itemInstance.SetActive(value: true);
                    self.m_visEquipment.CleanupInstance(itemInstance);

                    itemInstance.transform.SetParent(self.m_visEquipment.m_rightHand);
                    itemInstance.transform.localPosition = Vector3.zero;
                    itemInstance.transform.localRotation = Quaternion.identity;
                    self.m_visEquipment.m_rightItemInstance = itemInstance;
                }
            }
            self.m_visEquipment.UpdateLodgroup();
        }

        private void InventoryGui_OnRightClickItem(On.InventoryGui.orig_OnRightClickItem orig, InventoryGui self, InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos)
        {
            if (grid.GetInventory() == this.GetInventory())
            {
                var itemType = item.m_shared.m_itemType;
                if (item.IsEquipable())
                {
                    if (m_npc.IsItemEquiped(item))
                    {
                        m_npc.UnequipItem(item);
                    }
                    else
                    {
                        m_isRightClick = true;
                        m_npc.EquipItem(item);
                        m_isRightClick = false;
                    }
                }
            }
            else
            {
                orig(self, grid, item, pos);
            }
        }

        public void Init(Inventory npcInventory)
        {
            this.m_humanoidInventory = npcInventory;
        }

        public bool NpcInteract(Humanoid character)
        {
            return base.Interact(character, false, false);
        }

        public new bool Interact(Humanoid character, bool hold, bool alt)
        {
            var tamable = gameObject.GetComponent<Tameable>();
            return tamable.Interact(character, hold, alt);
        }
    }
}
