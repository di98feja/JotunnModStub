using HarmonyLib;
using Jotunn.GUI;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static RagnarsRokare.Factions.InteractionManager;
using Logger = Jotunn.Logger;

namespace RagnarsRokare.Factions
{
    internal static class WorkAssignmentsDialog
    {
        internal static void AddWorkAssignmentsDialog(ZDO npcZdo, Player player, List<Response> responses)
        {
            responses.Add(new Response
            {
                Text = "I have some work for you...",
                Callback = () =>
                {
                    InteractionPanel.SetActive(false);
                    GUIManager.BlockInput(false);
                    CreateWorkAssignmentsDialog(npcZdo, player);
                    InteractionPanel.SetActive(true);
                    GUIManager.BlockInput(true);
                }
            });
        }

        private static void CreateWorkAssignmentsDialog(ZDO npcZdo, Player player)
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
            var npcAi = MobAI.MobManager.AliveMobs[npcZdo.GetString(Constants.Z_UniqueId)] as NpcAI;

            var allAssignmentTypes = MobAI.Assignment.AssignmentTypes.ToList();
            allAssignmentTypes.Add(new MobAI.AssignmentType { Name = Misc.Constants.SortingAssignmentName });
            allAssignmentTypes.Add(new MobAI.AssignmentType { Name = Misc.Constants.RepairingAssignmentName });

            // Create the panel object
            InteractionPanel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0, -10),
                width: 550,
                height: 100 + 60 * allAssignmentTypes.Count,
                draggable: false);
            InteractionPanel.SetActive(false);

            // Add the Jötunn draggable Component to the panel
            // Note: This is normally automatically added when using CreateWoodpanel()
            InteractionPanel.AddComponent<DragWindowCntrl>();

            // Create the text object
            GUIManager.Instance.CreateText(
                text: "Select what tasks to perform",
                parent: InteractionPanel.transform,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                position: new Vector2(0f, -40f),
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: 20,
                color: GUIManager.Instance.ValheimOrange,
                outline: false,
                outlineColor: Color.black,
                width: 500f,
                height: 60f,
                addContentSizeFitter: false);

            float lineYPos = 100f;
            foreach (var assignmentType in allAssignmentTypes)
            {
                CreateToggle(lineYPos, assignmentType.Name, npcAi.m_trainedAssignments.Contains(assignmentType.Name), (a) =>
                {
                    if (a && !npcAi.m_trainedAssignments.Contains(assignmentType.Name))
                    {
                        npcAi.m_trainedAssignments.Add(assignmentType.Name);
                    }
                    else if (!a && npcAi.m_trainedAssignments.Contains(assignmentType.Name))
                    {
                        npcAi.m_trainedAssignments.Remove(assignmentType.Name);
                    }
                });
                lineYPos += 50f;
            }
            CreateToggle(lineYPos, "All", false, (state) =>
            {
                foreach (var t in InteractionPanel.GetComponentsInChildren<Toggle>())
                {
                    t.SetIsOnWithoutNotify(state);
                    npcAi.m_trainedAssignments.Clear();
                    if (state)
                    {
                        npcAi.m_trainedAssignments.AddRange(allAssignmentTypes.Select(a => a.Name));
                    }
                }
            });
            // Add a listener to the button to close the panel again
            GameObject greetButton = GUIManager.Instance.CreateButton(
                text: "Close",
                parent: InteractionPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(0, 50),
                width: 500f,
                height: 60f);
            greetButton.SetActive(true);
            Button button = greetButton.GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                InteractionPanel.SetActive(false);
                GUIManager.BlockInput(false);
                npcAi.NView.GetZDO().Set(Constants.Z_trainedAssignments, npcAi.m_trainedAssignments.Join());
                npcAi.NView.InvokeRPC(ZNetView.Everybody, Constants.Z_updateTrainedAssignments, npcAi.UniqueID, npcAi.m_trainedAssignments.Join());
            });
        }

        private static void CreateToggle(float lineYPos, string text, bool state, UnityAction<bool> callback)
        {
            GameObject assignmentToggle = GUIManager.Instance.CreateToggle(
                parent: InteractionPanel.transform,
                width: 30f,
                height: 30f);
            var tTransform = assignmentToggle.transform as RectTransform;
            tTransform.anchoredPosition = new Vector2(100f, lineYPos + 40);
            tTransform.anchorMin = new Vector2(0f, 0f);
            tTransform.anchorMax = new Vector2(0f, 0f);
            var toggle = assignmentToggle.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(state);
            toggle.onValueChanged.AddListener(callback);
            var toggleText = assignmentToggle.GetComponentInChildren<Text>();
            toggleText.text = text;
            toggleText.fontSize = 20;
            toggleText.font = GUIManager.Instance.AveriaSerifBold;
            toggleText.color = GUIManager.Instance.ValheimOrange;
            toggleText.enabled = true;
            toggleText.rectTransform.sizeDelta = new Vector2(400f, 0f);
            toggleText.rectTransform.pivot = new Vector2(0f, 0f);
            toggleText.rectTransform.localPosition = new Vector3(50f, -10f);
        }
    }
}
