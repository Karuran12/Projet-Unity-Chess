using UnityEngine;

namespace ChessDeck
{
    public class PlayerLoadout : MonoBehaviour
    {
        public static PlayerLoadout I;
        public PieceDefinition[] whiteMajors = new PieceDefinition[4];
        public PieceDefinition[] blackMajors = new PieceDefinition[4];

        void Awake()
        {
            if (I != null) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool IsReady()
        {
            for (int i=0;i<4;i++)
            {
                if (whiteMajors[i] == null) return false;
                if (blackMajors[i] == null) return false;
            }
            return true;
        }
    }
}
