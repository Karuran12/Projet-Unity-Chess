using UnityEngine;

namespace ChessDeck
{
    public enum MoveKind { Slide, Step }

    public enum SpecialType
    {
        None,
        Thief,
        SkeletonGiant,
        Etourdie,
        Archer,
        Twins,
        Serpent,
        General,
        Commander
    }

    [CreateAssetMenu(fileName = "PieceDef", menuName = "ChessDeck/Piece Definition")]
    public class PieceDefinition : ScriptableObject
    {
        [Header("Base")]
        public string displayName = "Piece";
        public Sprite sprite;
        public bool isKing = false;
        public bool isPawn = false;
        public int cost = 1;

        [Header("Sprites par équipe")]
        public Sprite whiteSprite;
        public Sprite blackSprite;

        [Header("Déplacements")]
        public MoveKind moveKind = MoveKind.Slide;
        public bool dirN, dirS, dirE, dirW, dirNE, dirNW, dirSE, dirSW;
        [Range(1, 8)] public int maxRange = 8;
        public Vector2Int[] stepOffsets;

        [Header("Spécial")]
        public SpecialType special = SpecialType.None;
    }
}
