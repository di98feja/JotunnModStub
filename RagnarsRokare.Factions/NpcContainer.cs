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
            //On.Humanoid.EquipItem += Humanoid_EquipItem;
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
                self.m_rightItem = item;
                self.m_rightItem.m_equiped = item != null;
                self.m_visEquipment.SetRightItem(item?.m_dropPrefab?.name);
                self.m_visEquipment.UpdateEquipmentVisuals();
            }
            return true;
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
