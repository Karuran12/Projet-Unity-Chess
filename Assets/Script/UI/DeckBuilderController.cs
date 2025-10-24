using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace ChessDeck
{
    public class DeckBuilderController : MonoBehaviour
    {
        [Header("UI - Liste des pièces")]
        public Transform piecesListParent;   
        public GameObject pieceButtonPrefab; 

        [Header("UI - Slots")]
        public Transform whiteSlotsParent;   
        public Transform blackSlotsParent;   
        public Button validateButton;        

        [Header("UI - Camp actif (optionnel)")]
        public TMP_Text sideLabel;            
        public Button toggleSideButton;       
        public CanvasGroup whiteGroup;       
        public CanvasGroup blackGroup;       

        const int SLOTS_PER_SIDE = 4;
        const int MAX_COPIES_PER_TYPE = 1;

        readonly List<PieceDefinition> allMajors = new List<PieceDefinition>();
        readonly List<PieceDefinition> whitePick = new List<PieceDefinition>(SLOTS_PER_SIDE);
        readonly List<PieceDefinition> blackPick = new List<PieceDefinition>(SLOTS_PER_SIDE);
        readonly Dictionary<string, int> whiteCounts = new Dictionary<string, int>();
        readonly Dictionary<string, int> blackCounts = new Dictionary<string, int>();

        bool pickingWhite = true;

        void Start()
        {
            LoadAllMajors();
            BuildPiecesButtons();
            AutoBindSlotButtons(); // rend les slots cliquables pour retirer
            RefreshUI();
            UpdateSideUI();
        }

        void LoadAllMajors()
        {
            allMajors.Clear();
            var defs = Resources.LoadAll<PieceDefinition>("Pieces");
            foreach (var d in defs)
            {
                if (!d) continue;
                if (d.isKing || d.isPawn) continue;
                allMajors.Add(d);
            }
            allMajors.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase));
        }

        void BuildPiecesButtons()
{
    foreach (Transform c in piecesListParent) Destroy(c.gameObject);

    foreach (var def in allMajors)
    {
        var go = Instantiate(pieceButtonPrefab, piecesListParent);
        go.name = $"Btn_{def.displayName}";

        var icon = go.transform.Find("Icon");
        if (icon) Destroy(icon.gameObject);

        var btn = go.GetComponent<Button>();
        if (!btn) btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => OnPick(def));

        var tmp = go.GetComponentInChildren<TMP_Text>();
        if (tmp) tmp.text = def.displayName;
        var utext = go.GetComponentInChildren<Text>();
        if (!tmp && utext) utext.text = def.displayName;
    }
}



        void AutoBindSlotButtons()
        {
            for (int i = 0; i < SLOTS_PER_SIDE && i < whiteSlotsParent.childCount; i++)
            {
                var b = whiteSlotsParent.GetChild(i).GetComponent<Button>();
                if (!b) b = whiteSlotsParent.GetChild(i).gameObject.AddComponent<Button>();
                int idx = i;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => RemoveSlot(true, idx));
            }
            for (int i = 0; i < SLOTS_PER_SIDE && i < blackSlotsParent.childCount; i++)
            {
                var b = blackSlotsParent.GetChild(i).GetComponent<Button>();
                if (!b) b = blackSlotsParent.GetChild(i).gameObject.AddComponent<Button>();
                int idx = i;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => RemoveSlot(false, idx));
            }
        }

        void OnPick(PieceDefinition def)
        {
            var pick   = pickingWhite ? whitePick   : blackPick;
            var counts = pickingWhite ? whiteCounts : blackCounts;

            string key = def.displayName;
            if (!counts.ContainsKey(key)) counts[key] = 0;

            if (counts[key] >= MAX_COPIES_PER_TYPE) return;
            if (pick.Count >= SLOTS_PER_SIDE) return;

            pick.Add(def);
            counts[key]++;

            RefreshUI();
        }

        public void RemoveSlot(bool whiteSide, int index)
        {
            var pick = whiteSide ? whitePick : blackPick;
            if (index < 0 || index >= pick.Count) return;

            var def = pick[index];
            pick.RemoveAt(index);

            var counts = whiteSide ? whiteCounts : blackCounts;
            if (!counts.ContainsKey(def.displayName)) counts[def.displayName] = 0;
            counts[def.displayName] = Mathf.Max(0, counts[def.displayName] - 1);

            RefreshUI();
        }

        public void ToggleSide()
        {
            pickingWhite = !pickingWhite;
            UpdateSideUI();
        }

        void UpdateSideUI()
        {
            if (sideLabel)
                sideLabel.text = pickingWhite ? "Camp : Blancs" : "Camp : Noirs";

            if (toggleSideButton)
            {
                var t = toggleSideButton.GetComponentInChildren<TMP_Text>();
                if (t) t.text = pickingWhite ? "Passer aux Noirs" : "Passer aux Blancs";
            }

            if (whiteGroup)
            {
                whiteGroup.alpha = pickingWhite ? 1f : 0.5f;
                whiteGroup.interactable = pickingWhite;
                whiteGroup.blocksRaycasts = pickingWhite;
            }
            if (blackGroup)
            {
                blackGroup.alpha = pickingWhite ? 0.5f : 1f;
                blackGroup.interactable = !pickingWhite;
                blackGroup.blocksRaycasts = !pickingWhite;
            }
        }

        void RefreshUI()
        {
            for (int i = 0; i < SLOTS_PER_SIDE; i++)
            {
                var w = whiteSlotsParent.GetChild(i);
                SetSlotLabel(w, i < whitePick.Count ? whitePick[i].displayName : "(vide)");

                var b = blackSlotsParent.GetChild(i);
                SetSlotLabel(b, i < blackPick.Count ? blackPick[i].displayName : "(vide)");
            }

            if (validateButton)
                validateButton.interactable = (whitePick.Count == SLOTS_PER_SIDE && blackPick.Count == SLOTS_PER_SIDE);
        }

        void SetSlotLabel(Transform slot, string label)
        {
            var tmp = slot.GetComponentInChildren<TMP_Text>();
            if (tmp) tmp.text = label;
            var utext = slot.GetComponentInChildren<Text>();
            if (utext) utext.text = label;
        }

        public void ValidateAndBackToMenu()
        {
            if (!PlayerLoadout.I)
            {
                Debug.LogError("PlayerLoadout manquant (scène MainMenu). Ajoute 'Systems' avec PlayerLoadout et lance depuis MainMenu.");
                return;
            }

            EnsureArraySize(PlayerLoadout.I.whiteMajors, SLOTS_PER_SIDE);
            EnsureArraySize(PlayerLoadout.I.blackMajors, SLOTS_PER_SIDE);

            for (int i = 0; i < SLOTS_PER_SIDE; i++)
            {
                PlayerLoadout.I.whiteMajors[i] = whitePick[i];
                PlayerLoadout.I.blackMajors[i] = blackPick[i];
            }

            SceneManager.LoadScene("Game");
        }

        void EnsureArraySize(PieceDefinition[] arr, int size)
        {
            if (arr == null || arr.Length != size)
            {
                Debug.LogWarning("Array de loadout de taille inattendue, mais on remplit jusqu'à 4.");
            }
        }
    }
}
