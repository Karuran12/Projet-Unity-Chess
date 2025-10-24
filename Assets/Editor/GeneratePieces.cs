#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ChessDeck.Editor
{
    public static class GeneratePieces
    {
        [MenuItem("Tools/ChessDeck/Generate PieceDefinitions")]
        public static void Run()
        {
            const string soFolder = "Assets/ScriptableObjects";
            if (!AssetDatabase.IsValidFolder(soFolder))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

            // Base set
            CreateKing(soFolder);
            CreatePawn(soFolder);

            // Déjà implémentées
            CreateThief(soFolder);
            CreateSkeletonGiant(soFolder);
            CreateEtourdie(soFolder);
            CreateArcher(soFolder);

            // Nouvelles
            CreateTwins(soFolder);
            CreateSerpent(soFolder);
            CreateGeneral(soFolder);
            CreateCommander(soFolder);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("ChessDeck", "PieceDefinitions générées dans Assets/ScriptableObjects", "OK");
        }

        // --------- utilitaire ---------
        static void CreateAsset(string path, System.Action<PieceDefinition> setup)
        {
            // évite d’écraser si déjà présent
            var existing = AssetDatabase.LoadAssetAtPath<PieceDefinition>(path);
            if (existing != null)
            {
                setup(existing);
                EditorUtility.SetDirty(existing);
                return;
            }

            var asset = ScriptableObject.CreateInstance<PieceDefinition>();
            setup(asset);
            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
        }

        // --------- Base ---------
        static void CreateKing(string folder)
        {
            CreateAsset($"{folder}/King.asset", def =>
            {
                def.displayName = "King";
                def.isKing = true;
                def.isPawn = false;
                def.cost = 0;

                def.moveKind = MoveKind.Slide;
                def.dirN = def.dirS = def.dirE = def.dirW = def.dirNE = def.dirNW = def.dirSE = def.dirSW = true;
                def.maxRange = 1;

                def.stepOffsets = null;
                def.special = SpecialType.None;
            });
        }

        static void CreatePawn(string folder)
        {
            CreateAsset($"{folder}/Pawn.asset", def =>
            {
                def.displayName = "Pawn";
                def.isKing = false;
                def.isPawn = true;
                def.cost = 0;

                // le mouvement du pion est géré côté Piece.cs (avances/captures diagonales)
                def.moveKind = MoveKind.Step;
                def.stepOffsets = new Vector2Int[] { new Vector2Int(0, 1) }; // indicatif
                def.dirN = def.dirS = def.dirE = def.dirW = def.dirNE = def.dirNW = def.dirSE = def.dirSW = false;
                def.maxRange = 1;

                def.special = SpecialType.None;
            });
        }

        // --------- Tes pièces déjà implémentées ---------
        static void CreateThief(string folder)
        {
            CreateAsset($"{folder}/Thief.asset", def =>
            {
                def.displayName = "Thief";
                def.cost = 3;

                def.moveKind = MoveKind.Step;
                def.stepOffsets = new Vector2Int[]
                {
                    new Vector2Int(0, 1),  new Vector2Int(0, -1),
                    new Vector2Int(1, 0),  new Vector2Int(-1, 0)
                };
                def.dirN = def.dirS = def.dirE = def.dirW = def.dirNE = def.dirNW = def.dirSE = def.dirSW = false;
                def.maxRange = 1;

                def.special = SpecialType.Thief;
            });
        }

        static void CreateSkeletonGiant(string folder)
        {
            CreateAsset($"{folder}/SkeletonGiant.asset", def =>
            {
                def.displayName = "Skeleton Giant";
                def.cost = 3;

                def.moveKind = MoveKind.Slide;
                def.dirN = def.dirS = def.dirE = def.dirW = def.dirNE = def.dirNW = def.dirSE = def.dirSW = true;
                def.maxRange = 1; // roi-like

                def.stepOffsets = null;
                def.special = SpecialType.SkeletonGiant;
            });
        }

        static void CreateEtourdie(string folder)
        {
            CreateAsset($"{folder}/Etourdie.asset", def =>
            {
                def.displayName = "Etourdie";
                def.cost = 2;

                def.moveKind = MoveKind.Slide;
                def.dirN = def.dirS = def.dirE = def.dirW = true;   // comme une tour
                def.dirNE = def.dirNW = def.dirSE = def.dirSW = false;
                def.maxRange = 8;

                def.stepOffsets = null;
                def.special = SpecialType.Etourdie;
            });
        }

        static void CreateArcher(string folder)
        {
            CreateAsset($"{folder}/Archer.asset", def =>
            {
                def.displayName = "Archer";
                def.cost = 2;

                def.moveKind = MoveKind.Slide;
                def.dirN = def.dirS = def.dirE = def.dirW = def.dirNE = def.dirNW = def.dirSE = def.dirSW = true;
                def.maxRange = 1; // déplacement comme un roi (tir géré en code)

                def.stepOffsets = null;
                def.special = SpecialType.Archer;
            });
        }

        // --------- Nouvelles pièces ---------
        static void CreateTwins(string folder)
        {
            CreateAsset($"{folder}/Twins.asset", def =>
            {
                def.displayName = "Twins";
                def.cost = 3;

                def.moveKind = MoveKind.Step; // déplacements de cavalier
                def.stepOffsets = new Vector2Int[]
                {
                    new Vector2Int(1,2), new Vector2Int(2,1), new Vector2Int(-1,2), new Vector2Int(-2,1),
                    new Vector2Int(1,-2), new Vector2Int(2,-1), new Vector2Int(-1,-2), new Vector2Int(-2,-1)
                };
                def.dirN = def.dirS = def.dirE = def.dirW = def.dirNE = def.dirNW = def.dirSE = def.dirSW = false;
                def.maxRange = 1;

                def.special = SpecialType.Twins;
            });
        }

        static void CreateSerpent(string folder)
        {
            CreateAsset($"{folder}/Serpent.asset", def =>
            {
                def.displayName = "Serpent";
                def.cost = 3;

                def.moveKind = MoveKind.Slide;
                def.dirNE = def.dirNW = def.dirSE = def.dirSW = true; // diagonales seulement
                def.dirN = def.dirS = def.dirE = def.dirW = false;
                def.maxRange = 3; // portée limitée à 3

                def.stepOffsets = null;
                def.special = SpecialType.Serpent;
            });
        }

        static void CreateGeneral(string folder)
        {
            CreateAsset($"{folder}/General.asset", def =>
            {
                def.displayName = "General";
                def.cost = 4;

                def.moveKind = MoveKind.Slide;
                def.dirN = def.dirS = def.dirE = def.dirW = def.dirNE = def.dirNW = def.dirSE = def.dirSW = true;
                def.maxRange = 1; // commence roi-like ; s’incrémente via Piece.generalRange

                def.stepOffsets = null;
                def.special = SpecialType.General;
            });
        }

        static void CreateCommander(string folder)
        {
            CreateAsset($"{folder}/Commander.asset", def =>
            {
                def.displayName = "Commander";
                def.cost = 4;

                def.moveKind = MoveKind.Slide;
                def.dirN = def.dirS = def.dirE = def.dirW = def.dirNE = def.dirNW = def.dirSE = def.dirSW = true;
                def.maxRange = 2; // déplacement court ; l'aura sera gérée en code

                def.stepOffsets = null;
                def.special = SpecialType.Commander;
            });
        }
    }
}
#endif
