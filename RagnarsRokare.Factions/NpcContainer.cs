using HarmonyLib;
using System;

namespace RagnarsRokare.Factions
{
    internal class NpcContainer : Container, Interactable
    {
        private Humanoid m_npc;
        private Inventory m_humanoidInventory;

        public static event EventHandler<InventoryHiddenEventArgs> InventoryHidden;
        public static event EventHandler<ItemRightClickedEventArgs> ItemRightClicked;
        private new void Awake()
        {
            base.Awake();
            m_nview.Unregister("OpenRespons");
            m_nview.Register<bool>("OpenRespons", RPC_OpenRespons);

            m_npc = gameObject.GetComponent<Humanoid>();
            InventoryHidden += NpcContainer_InventoryHidden;
            ItemRightClicked += NpcContainer_ItemRightClicked;
        }

        private void NpcContainer_ItemRightClicked(object sender, ItemRightClickedEventArgs e)
        {
            if (e.Grid.GetInventory() == this.GetInventory())
            {
                var itemType = e.Item.m_shared.m_itemType;
                if (e.Item.IsEquipable())
                {
                    if (m_npc.IsItemEquiped(e.Item))
                    {
                        m_npc.UnequipItem(e.Item);
                    }
                    else
                    {
                        m_npc.EquipItem(e.Item);
                    }
                }
                e.WasHandled = true;
            }
            else
            {
                e.WasHandled = false;
            }
        }

        private void NpcContainer_InventoryHidden(object sender, InventoryHiddenEventArgs e)
        {
            if (m_humanoidInventory != null && e.Instance.m_currentContainer?.m_inventory == m_humanoidInventory)
            {
                Save(m_npc);
            }
        }

        private static void OnInventoryHidden(InventoryHiddenEventArgs e)
        {
            InventoryHidden?.Invoke(null, e);
        }

        private static void OnItemRightClicked(ItemRightClickedEventArgs e)
        {
            ItemRightClicked?.Invoke(null, e);
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide)), HarmonyPrefix]
        private static void InventoryGui_Hide(InventoryGui __instance)
        {
            OnInventoryHidden(new InventoryHiddenEventArgs(__instance));
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

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnRightClickItem)), HarmonyPrefix]
        private static bool InventoryGui_OnRightClickItem(InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos)
        {
            var eventArgs = new ItemRightClickedEventArgs(grid, item, pos);
            OnItemRightClicked(eventArgs);            
            return !eventArgs.WasHandled;
        }

        public void Load(Humanoid npc)
        {
            this.m_humanoidInventory = npc.GetInventory();
            var nview = npc.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
            {
                var savedInventory = nview.GetZDO().GetByteArray(Misc.Constants.Z_SavedInventory);
                if (savedInventory != null)
                {
                    Jotunn.Logger.LogDebug($"{npc.GetHoverName()}:Loading inventory");
                    m_humanoidInventory.Load(new ZPackage(savedInventory));
                }
            }
        }

        public void Save(Humanoid npc)
        {
            var nview = npc.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
            {
                Jotunn.Logger.LogDebug($"{npc.GetHoverName()}:Saving inventory");
                var savedInventory = new ZPackage();
                m_humanoidInventory.Save(savedInventory);
                nview.GetZDO().Set(Misc.Constants.Z_SavedInventory, savedInventory.GetArray());
            }
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

        public class InventoryHiddenEventArgs: EventArgs
        {
            public InventoryGui Instance { get; set; }

            public InventoryHiddenEventArgs(InventoryGui instance)
            {
                this.Instance = instance;
            }
        }

        public class ItemRightClickedEventArgs : EventArgs
        {
            public InventoryGrid Grid { get; set; }
            public ItemDrop.ItemData Item { get; set; }
            public Vector2i Pos { get; set; }
            public bool WasHandled { get; set; }

            public ItemRightClickedEventArgs(InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos)
            {
                this.Grid = grid;
                this.Item = item;
                this.Pos = pos;
            }
        }
    }
}
