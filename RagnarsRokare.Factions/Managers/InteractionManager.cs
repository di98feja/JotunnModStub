using Jotunn.GUI;
using Jotunn.Managers;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Logger = Jotunn.Logger;

namespace RagnarsRokare.Factions
{
    internal static class InteractionManager
    {
        public static UnityEngine.GameObject InteractionPanel { get; private set; }

        public static void TogglePanel(Character npc)
        {
            // Create the panel if it does not exist
            if (!InteractionPanel)
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
                    height: 600,
                    draggable: false);
                InteractionPanel.SetActive(false);

                // Add the Jötunn draggable Component to the panel
                // Note: This is normally automatically added when using CreateWoodpanel()
                InteractionPanel.AddComponent<DragWindowCntrl>();

                // Create the text object
                GUIManager.Instance.CreateText(
                    text: npc.GetHoverName(),
                    parent: InteractionPanel.transform,
                    anchorMin: new Vector2(0.5f, 1f),
                    anchorMax: new Vector2(0.5f, 1f),
                    position: new Vector2(0f, -50f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 30,
                    color: GUIManager.Instance.ValheimOrange,
                    outline: true,
                    outlineColor: Color.black,
                    width: 350f,
                    height: 40f,
                    addContentSizeFitter: false);

                // Create the button object
                GameObject buttonObject = GUIManager.Instance.CreateButton(
                    text: "A Test Button - long dong schlongsen text",
                    parent: InteractionPanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0, -250f),
                    width: 250f,
                    height: 60f);
                buttonObject.SetActive(true);

                // Add a listener to the button to close the panel again
                Button button = buttonObject.GetComponent<Button>();
                button.onClick.AddListener(() =>
                {
                    InteractionPanel.SetActive(false);
                    GUIManager.BlockInput(false);
                });

                // Create a dropdown
                var dropdownObject = GUIManager.Instance.CreateDropDown(
                    parent: InteractionPanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(-250f, -250f),
                    fontSize: 16,
                    width: 100f,
                    height: 30f);
                dropdownObject.GetComponent<Dropdown>().AddOptions(new List<string>
        {
            "bla", "blubb", "börks", "blarp", "harhar"
        });

                // Create an input field
                GUIManager.Instance.CreateInputField(
                    parent: InteractionPanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(250f, -250f),
                    contentType: InputField.ContentType.Standard,
                    placeholderText: "input...",
                    fontSize: 16,
                    width: 160f,
                    height: 30f);
            }

            // Switch the current state
            bool state = !InteractionPanel.activeSelf;

            // Set the active state of the panel
            InteractionPanel.SetActive(state);

            // Toggle input for the player and camera while displaying the GUI
            GUIManager.BlockInput(state);
        }
    }
}
