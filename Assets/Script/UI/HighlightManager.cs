using System.Collections.Generic;
using UnityEngine;

namespace ChessDeck
{
    public class HighlightManager : MonoBehaviour
    {
        public static HighlightManager I;

        [Header("Prefabs")]
        public GameObject moveMarkerPrefab;
        public GameObject captureMarkerPrefab;

        [Header("Board ref")]
        public BoardManager board;
        
        readonly List<GameObject> active = new List<GameObject>();
        readonly Stack<GameObject> poolMove = new Stack<GameObject>();
        readonly Stack<GameObject> poolCap = new Stack<GameObject>();

        void Awake()
        {
            if (I != null) { Destroy(gameObject); return; }
            I = this;
        }

        public void Clear()
        {
            foreach (var go in active) { go.SetActive(false);
                if (go.name.Contains("(Move)")) poolMove.Push(go);
                else poolCap.Push(go);
            }
            active.Clear();
        }

        GameObject GetFromPool(bool capture)
        {
            var stack = capture ? poolCap : poolMove;
            if (stack.Count > 0) { var go = stack.Pop(); go.SetActive(true); return go; }
            var prefab = capture ? captureMarkerPrefab : moveMarkerPrefab;
            var inst = Instantiate(prefab, transform);
            inst.name = capture ? "CaptureMarker (Cap)" : "MoveMarker (Move)";
            return inst;
        }

        public void ShowMoves(IEnumerable<MoveCandidate> moves)
        {
            if (!board) board = Object.FindFirstObjectByType<BoardManager>();
            Clear();
            foreach (var m in moves)
            {
                bool isCap = m.isCapture || m.isRangedCapture;
                var go = GetFromPool(isCap);
                var pos = new Vector3(m.to.x * board.cellSize, m.to.y * board.cellSize, 0f);
                go.transform.position = pos;
                active.Add(go);
            }
        }
    }
}
