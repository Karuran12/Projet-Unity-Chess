using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ChessDeck
{
    public class DeckBuilderController : MonoBehaviour
    {
        [Header("UI")]
        public Transform piecesListParent;    // ScrollView/Viewport/Content
        public GameObject pieceButtonPrefab;  // Button avec un composant Text ou TMP_Text
        public Transform whiteSlotsParent;    // 4 enfants
        public Transform blackSlotsParent;    // 4 enfants
        public Button validateButton;

        const int SLOTS_PER_SIDE = 4;
        const int MAX_COPIES_PER_TYPE = 2;

        List<PieceDefinition> allMajors = new List<PieceDefinition>();
        List<PieceDefinition> whitePick = new List<PieceDefinition>(SLOTS_PER_SIDE);
        List<PieceDefinition> blackPick = new List<PieceDefinition>(SLOTS_PER_SIDE);
        Dictionary<string,int> whiteCounts = new Dictionary<string,int>();
        Dictionary<string,int> blackCounts = new Dictionary<string,int>();

        bool pickingWhite = true; // toggle camp courant (si tu veux un bouton pour changer)

        void Start()
        {
            LoadAllMajors();
            BuildPiecesButtons();
            RefreshUI();
        }

        void LoadAllMajors()
        {
            var defs = Resources.LoadAll<PieceDefinition>("Pieces");
            foreach (var d in defs)
            {
                if (d == null) continue;
                if (d.isKing || d.isPawn) continue;
                allMajors.Add(d);
            }
        }

        void BuildPiecesButtons()
        {
            foreach (Transform c in piecesListParent) Destroy(c.gameObject);
            foreach (var def in allMajors)
            {
                var go = Instantiate(pieceButtonPrefab, piecesListParent);
                go.name = $"Btn_{def.displayName}";
                var btn = go.GetComponent<Button>();

                var tmp = go.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmp) tmp.text = def.displayName;
                var utext = go.GetComponentInChildren<Text>();
                if (!tmp && utext) utext.text = def.displayName;

                btn.onClick.AddListener(() => OnPick(def));
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
            counts[def.displayName] = Mathf.Max(0, counts[def.displayName] - 1);

            RefreshUI();
        }

        public void ToggleSide()
        {
            pickingWhite = !pickingWhite;
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
            var tmp = slot.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp) tmp.text = label;
            var utext = slot.GetComponentInChildren<Text>();
            if (utext) utext.text = label;
        }

        public void ValidateAndBackToMenu()
        {
            if (!PlayerLoadout.I) { Debug.LogError("PlayerLoadout manquant dans MainMenu."); return; }

            for (int i = 0; i < SLOTS_PER_SIDE; i++)
            {
                PlayerLoadout.I.whiteMajors[i] = whitePick[i];
                PlayerLoadout.I.blackMajors[i] = blackPick[i];
            }

            SceneManager.LoadScene("MainMenu");
        }
    }
}
