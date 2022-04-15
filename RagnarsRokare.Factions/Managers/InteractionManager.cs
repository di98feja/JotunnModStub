using Jotunn.GUI;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Logger = Jotunn.Logger;

namespace RagnarsRokare.Factions
{
    internal static class InteractionManager
    {
        public static UnityEngine.GameObject InteractionPanel { get; private set; }

        public static void StartNpcInteraction(Character npc)
        {
            var npcZdo = MobAI.Common.GetNView(npc)?.GetZDO();
            if (npcZdo == null)
            {
                Logger.LogError($"Could not get ZDO from {npc.GetHoverName()}, can't interact");
                return;
            }
            var playerStanding = StandingsManager.GetStandingTowards(npcZdo, FactionManager.GetLocalPlayerFaction());
            if (playerStanding < Misc.Constants.Standing_MinimumInteraction)
            {
                Logger.LogDebug($"{npc.GetHoverName()}: Player standing to low: {playerStanding}");
                return;
            }

            if (!HasGotRealName(npcZdo))
            {
                NpcManager.CreateAndSetRandomNameForNpc(npc);
                ShowGreetPlayerDialog(npcZdo, Player.m_localPlayer);
                return;
            }

            string npcText = "Yes?";
            var responses = new List<Response>();

            var motivationLevel = npcZdo.GetFloat(Misc.Constants.Z_MotivationLevel);
            if (motivationLevel > Misc.Constants.Motivation_Apathy)
            {
                AddErrandDialog(npcZdo, Player.m_localPlayer, responses);
            }
            if (FactionManager.IsSameFaction(npcZdo, Player.m_localPlayer))
            {
                AddAccessInventoryDialog(npc, Player.m_localPlayer, responses);
            }

            CreateInteractionDialog(npcText, responses.ToArray());
            InteractionPanel.SetActive(true);
            GUIManager.BlockInput(true);
        }

        private static void AddAccessInventoryDialog(Character npc, Player m_localPlayer, List<Response> responses)
        {
            responses.Add(new Response
            {
                Text = "Show me your inventory",
                Callback = () =>
                {
                    InteractionPanel.SetActive(false);
                    GUIManager.BlockInput(false);
                    var npcInventory = npc.gameObject.GetComponent<NpcContainer>();
                    npcInventory.NpcInteract(npc as Humanoid);
                }
            });
        }

        private static void AddErrandDialog(ZDO npcZdo, Player player, List<Response> responses)
        {
            if (ErrandsManager.HasErrand(npcZdo, player))
            {
                responses.Add(new Response
                {
                    Text = "Regarding your request...",
                    Callback = () =>
                    {
                        InteractionPanel.SetActive(false);
                        GUIManager.BlockInput(false);
                        ShowErrandDialog(npcZdo, player);
                    }
                });
            }
            else
            {
                responses.Add(new Response
                {
                    Text = "Do you need any help?",
                    Callback = () =>
                    {
                        InteractionPanel.SetActive(false);
                        GUIManager.BlockInput(false);
                        ShowErrandDialog(npcZdo, player);
                    }
                });
            }
        }

        private static void ShowErrandDialog(ZDO npcZdo, Player player)
        {
            string npcText = String.Empty;
            List<Response> responses = new List<Response>();
            if (ErrandsManager.HasErrand(npcZdo, player))
            {
                npcText = "Yes? How did it go?";
                responses.Add(new Response
                {
                    Text = "On second thought, I no longer wish to complete your request",
                    Callback = () =>
                    {
                        InteractionPanel.SetActive(false);
                        string npcResponse = ErrandsManager.CancelErrand(npcZdo);
                        CreateInteractionDialog(npcResponse, new Response[]
                        {
                            new Response
                            {
                                Text = "Farewell",
                                Callback = () =>
                                {
                                    InteractionPanel.SetActive(false);
                                    GUIManager.BlockInput(false);
                                }
                            }
                        });
                        InteractionPanel.SetActive(true);
                    }
                });
                if (ErrandsManager.CanCompleteErrand(npcZdo, player))
                {
                    responses.Add(new Response
                    {
                        Text = "I have the things you requested",
                        Callback = () =>
                        {
                            InteractionPanel.SetActive(false);
                            string npcResponse = ErrandsManager.CompleteErrand(npcZdo, player);
                            CreateInteractionDialog(npcResponse, new Response[]
                            {
                                new Response
                                {
                                    Text = "Odins blessings",
                                    Callback = () =>
                                    {
                                        InteractionPanel.SetActive(false);
                                        GUIManager.BlockInput(false);
                                    }
                                }
                            });
                            InteractionPanel.SetActive(true);
                        }
                    });
                }
                responses.Add(new Response
                {
                    Text = "Oh, never mind",
                    Callback = () =>
                    {
                        InteractionPanel.SetActive(false);
                        GUIManager.BlockInput(false);
                    }
                });
            }
            else
            {
                var errand = ErrandsManager.GetRandomErrand();
                npcText = Localization.instance.Localize(errand.RequestString);
                responses.Add(new Response
                {
                    Text = Localization.instance.Localize($"Alright, consider it done. (Bring {errand.RequestItemAmount} {errand.RequestItem.m_shared.m_name})"),
                    Callback = () =>
                    {
                        InteractionPanel.SetActive(false);
                        GUIManager.BlockInput(false);
                        ErrandsManager.StartErrand(errand.Id, npcZdo, player);
                    }
                });

                responses.Add(new Response
                {
                    Text = "No, I have better thigs to do",
                    Callback = () =>
                    {
                        InteractionPanel.SetActive(false);
                        GUIManager.BlockInput(false);
                    }
                });
            }
            CreateInteractionDialog(npcText, responses.ToArray());
            InteractionPanel.SetActive(true);
            GUIManager.BlockInput(true);
        }

        private static void ShowGreetPlayerDialog(ZDO npcZdo, Player m_localPlayer)
        {
            var response = new Response
            {
                Text = $"Well met {npcZdo.GetString(Constants.Z_GivenName)}, I am {m_localPlayer.GetHoverName()}",
                Callback = () =>
                {
                    InteractionPanel.SetActive(false);
                    GUIManager.BlockInput(false);
                }
            };
            var response2 = new Response
            {
                Text = $"Sod off {npcZdo.GetString(Constants.Z_GivenName)}",
                Callback = () =>
                {
                    InteractionPanel.SetActive(false);
                    GUIManager.BlockInput(false);
                }
            };

            CreateInteractionDialog($"Greetings stranger, I am {npcZdo.GetString(Constants.Z_GivenName)}", new Response[] {response, response2});
            InteractionPanel.SetActive(true);
            GUIManager.BlockInput(true);
        }

        private static void CreateInteractionDialog(string npcSays, Response[] responses)
        {
            if (GUIManager.Instance == null)
            {
                Logger.LogError("GUIManager instance is null");
                return;
            }

            if (!GUIManager.CustomGUIFront)
            {
                Logger.LogError("GUIManager CustomGUI is null");
                return;
            }

            // Create the panel object
            InteractionPanel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0, 0),
                width: 850,
                height: 100 + 80 * responses.Length,
                draggable: false);
            InteractionPanel.SetActive(false);

            // Add the Jötunn draggable Component to the panel
            // Note: This is normally automatically added when using CreateWoodpanel()
            InteractionPanel.AddComponent<DragWindowCntrl>();

            // Create the text object
            GUIManager.Instance.CreateText(
                text: npcSays,
                parent: InteractionPanel.transform,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                position: new Vector2(0f, -40f),
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: 20,
                color: GUIManager.Instance.ValheimOrange,
                outline: false,
                outlineColor: Color.black,
                width: 800f,
                height: 60f,
                addContentSizeFitter: false);

            float buttonYPos = 60f;
            foreach (var response in responses)
            {
                // Create the button object
                GameObject greetButton = GUIManager.Instance.CreateButton(
                    text: response.Text,
                    parent: InteractionPanel.transform,
                    anchorMin: new Vector2(0.5f, 0f),
                    anchorMax: new Vector2(0.5f, 0f),
                    position: new Vector2(0, buttonYPos),
                    width: 800f,
                    height: 60f);
                greetButton.SetActive(true);

                // Add a listener to the button to close the panel again
                Button button = greetButton.GetComponent<Button>();
                button.onClick.AddListener(() => response.Callback());

                buttonYPos += 60f;
            }
        }

        private static bool HasGotRealName(ZDO npcZdo)
        {
            return !string.IsNullOrEmpty(npcZdo.GetString(Constants.Z_GivenName));
        }

        private class Response
        {
            public string Text { get; set; }
            public Action Callback { get; set; } = null;
        }
    }
}
