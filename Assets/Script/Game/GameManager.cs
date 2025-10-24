using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChessDeck
{
    public class GameManager : MonoBehaviour
    {
        [Header("Refs")]
        public BoardManager board;
        public PieceDefinition kingDef;
        public PieceDefinition pawnDef;

        [Header("Decks (4 majeures par camp)")]
        public PieceDefinition[] whiteMajors = new PieceDefinition[4];
        public PieceDefinition[] blackMajors = new PieceDefinition[4];

        [Header("État")]
        public Piece[,] occ = new Piece[8, 8];
        public Team currentTurn = Team.White;

        private Piece selected;
        private List<MoveCandidate> currentMoves = new List<MoveCandidate>();

        // Mémoire de la dernière pièce capturée (pour le Voleur)
        private PieceDefinition lastCapturedDef;
        private Team lastCapturedTeam;

        // Mouvement couplé des Jumelles (appliqué en fin d'exécution)
        (Piece piece, Vector2Int to)? pendingTwinMove = null;

        // -------------------- Lifecycle --------------------

        void Start()
        {
            if (!board) board = Object.FindFirstObjectByType<BoardManager>();

            // Récupère les decks depuis PlayerLoadout s’il existe
            var load = PlayerLoadout.I;
            if (load != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (i < load.whiteMajors.Length) whiteMajors[i] = load.whiteMajors[i];
                    if (i < load.blackMajors.Length) blackMajors[i] = load.blackMajors[i];
                }
            }

            ResetPosition();
        }

        void Update()
        {
            HandleClick();
        }

        // -------------------- Setup --------------------

        void ResetPosition()
        {
            // Vide l'échiquier
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    if (occ[x, y] != null)
                    {
                        Destroy(occ[x, y].gameObject);
                        occ[x, y] = null;
                    }
                }

            // Pions
            for (int x = 0; x < 8; x++)
            {
                Spawn(pawnDef, Team.White, x, 1);
                Spawn(pawnDef, Team.Black, x, 6);
            }

            // Rois
            Spawn(kingDef, Team.White, 4, 0);
            Spawn(kingDef, Team.Black, 4, 7);

            // Majeures (4 emplacements par défaut)
            int[] cols = { 0, 1, 6, 7 };
            for (int i = 0; i < 4 && i < whiteMajors.Length; i++)
                if (whiteMajors[i]) Spawn(whiteMajors[i], Team.White, cols[i], 0);
            for (int i = 0; i < 4 && i < blackMajors.Length; i++)
                if (blackMajors[i]) Spawn(blackMajors[i], Team.Black, cols[i], 7);

            currentTurn = Team.White;
            selected = null;
            currentMoves.Clear();
            HighlightManager.I?.Clear();
        }

        void Spawn(PieceDefinition def, Team team, int x, int y)
        {
            var go = new GameObject($"{def.displayName}_{team}_{x}_{y}");
            go.transform.parent = board.transform;
            var p = go.AddComponent<Piece>();
            p.Init(def, team, x, y, board);
            occ[x, y] = p;

            // Jumelles : si une seule paire par camp, on peut fixer pairId=0
            if (def.special == SpecialType.Twins)
            {
                p.pairId = 0;
            }
        }

        // -------------------- Input & Surbrillance --------------------

        void HandleClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            var wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            int gx = Mathf.FloorToInt((wp.x + board.cellSize * 0.5f) / board.cellSize);
            int gy = Mathf.FloorToInt((wp.y + board.cellSize * 0.5f) / board.cellSize);
            if (!board.InBounds(gx, gy)) return;

            var clicked = occ[gx, gy];

            // A) Pas de sélection -> tenter de sélectionner une pièce du camp courant
            if (selected == null)
            {
                if (clicked != null && clicked.team == currentTurn)
                {
                    selected = clicked;
                    currentMoves = GetLegalMoves(selected);
                    HighlightManager.I?.ShowMoves(currentMoves);
                }
                return;
            }

            // B) Déjà une sélection
            // B1) Cliquer une autre pièce alliée -> changer de sélection
            if (clicked != null && clicked.team == currentTurn)
            {
                selected = clicked;
                currentMoves = GetLegalMoves(selected);
                HighlightManager.I?.ShowMoves(currentMoves);
                return;
            }

            // B2) Tenter de jouer un coup vers (gx, gy)
            MoveCandidate? choice = null;
            foreach (var m in currentMoves)
            {
                if (m.to.x == gx && m.to.y == gy) { choice = m; break; }
            }

            // Coup illégal -> désélection
            if (!choice.HasValue)
            {
                selected = null;
                currentMoves.Clear();
                HighlightManager.I?.Clear();
                return;
            }

            // Coup légal -> exécution
            ExecuteMove(selected, choice.Value);

            // Nettoyer UI et passer le tour
            HighlightManager.I?.Clear();
            currentMoves.Clear();
            selected = null;

            EndTurn();
        }

        // -------------------- Règles : coups légaux --------------------

        List<MoveCandidate> GetLegalMoves(Piece piece)
        {
            var all = piece.GetMoves(occ).ToList();
            var legal = new List<MoveCandidate>();

            foreach (var mc in all)
            {
                var snap = Rules.Snapshot(occ);                           // copie logique
                Rules.ApplyMoveLogic(board, snap, piece, mc);             // applique le coup
                bool inCheck = Rules.IsKingInCheck(board, snap, piece.team); // roi du joueur en échec ?
                if (!inCheck) legal.Add(mc);
            }
            return legal;
        }

        // -------------------- Utilitaires --------------------

        Piece FindTwinSibling(Piece p)
        {
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                var q = occ[x, y];
                if (q == null || q == p) continue;
                if (q.team == p.team && q.def.special == SpecialType.Twins && q.pairId == p.pairId)
                    return q;
            }
            return null;
        }

        (Vector2Int r1, Vector2Int r2) RecoilSquaresFromOldPos(Vector2Int from, Vector2Int to)
        {
            int dx = Mathf.Clamp(to.x - from.x, -1, 1);
            int dy = Mathf.Clamp(to.y - from.y, -1, 1);
            var r1 = new Vector2Int(to.x - dx, to.y - dy);
            var r2 = new Vector2Int(to.x - 2 * dx, to.y - 2 * dy);
            return (r1, r2);
        }

        (Vector2Int r1, Vector2Int r2) RecoilSquares(Piece piece, MoveCandidate mc)
        {
            int dx = Mathf.Clamp(mc.to.x - piece.x, -1, 1);
            int dy = Mathf.Clamp(mc.to.y - piece.y, -1, 1);
            var r1 = new Vector2Int(piece.x - dx, piece.y - dy);
            var r2 = new Vector2Int(piece.x - 2 * dx, piece.y - 2 * dy);
            return (r1, r2);
        }

        // -------------------- Exécution d’un coup --------------------

        void ExecuteMove(Piece piece, MoveCandidate mc)
        {
            pendingTwinMove = null;

            // --- JUMELLES : si la sœur est vivante, vérifier le déplacement miroir est possible
            if (piece.def.special == SpecialType.Twins && !piece.twinLost)
            {
                var sister = FindTwinSibling(piece);
                if (sister == null) piece.twinLost = true;
                else
                {
                    int dx = mc.to.x - piece.x;
                    int dy = mc.to.y - piece.y;

                    // Miroir horizontal simple : la sœur applique (-dx, +dy)
                    int sx = sister.x - dx;
                    int sy = sister.y + dy;

                    bool legal = board.InBounds(sx, sy);
                    if (legal)
                    {
                        var targetSis = occ[sx, sy];
                        if (targetSis != null && targetSis.team == sister.team)
                            legal = false; // ne peut pas capturer une alliée
                    }

                    if (!legal) return; // le mouvement entier est annulé
                    pendingTwinMove = (sister, new Vector2Int(sx, sy));
                }
            }

            // --- 1) Archer : tir à distance (ne bouge pas)
            if (piece.def.special == SpecialType.Archer && mc.isRangedCapture)
            {
                var target = occ[mc.to.x, mc.to.y];
                if (target != null && target.team != piece.team)
                {
                    lastCapturedDef = target.def;
                    lastCapturedTeam = target.team;

                    target.OnCaptured(occ, this, mc.to); // explosion si squelette
                    Destroy(target.gameObject);
                    occ[mc.to.x, mc.to.y] = null;
                }
                // Général n’augmente pas de portée s’il ne s’est pas déplacé
                return;
            }

            // Mémoriser la case d’origine (utile pour calculer le recul Étourdie)
            Vector2Int origin = new Vector2Int(piece.x, piece.y);

            // --- 2) Capture au contact : traiter avant déplacement (cas Squelette & Serpent)
            bool capturedSkeleton = false;
            Piece captured = null;

            if (mc.isCapture)
            {
                captured = occ[mc.to.x, mc.to.y];
                if (captured != null && captured.team != piece.team)
                {
                    // ----- SERPENT : tenter transformation au lieu de destruction
                    if (piece.def.special == SpecialType.Serpent)
                    {
                        Vector2Int back = captured.previousPos; // case d’avant
                        bool canTransform = board.InBounds(back.x, back.y) && occ[back.x, back.y] == null;
                        if (canTransform)
                        {
                            var goPawn = new GameObject($"PoisonedPawn_{piece.team}");
                            goPawn.transform.parent = board.transform;
                            var np = goPawn.AddComponent<Piece>();
                            np.Init(pawnDef, piece.team, back.x, back.y, board);
                            occ[back.x, back.y] = np;

                            // On supprime la cible sur la case capturée (considérée vide)
                            lastCapturedDef = captured.def;
                            lastCapturedTeam = captured.team;
                            Destroy(captured.gameObject);
                            occ[mc.to.x, mc.to.y] = null;
                            captured = null; // déjà gérée
                        }
                        // Sinon : capture normale → on continue le flux
                    }

                    if (captured != null)
                    {
                        lastCapturedDef = captured.def;
                        lastCapturedTeam = captured.team;

                        if (captured.def.special == SpecialType.SkeletonGiant)
                            capturedSkeleton = true;

                        // Effets "à la mort"
                        captured.OnCaptured(occ, this, mc.to);

                        // Détruire la cible
                        Destroy(captured.gameObject);
                        occ[mc.to.x, mc.to.y] = null;
                    }
                }
            }

            // --- 2.bis) Si on a capturé un Géant Squelette :
            // - la case de mort reste vide
            // - l'attaquant NE s'y déplace pas
            if (capturedSkeleton)
            {
                // Étourdie : appliquer le recul si requis (depuis la position d'origine)
                if (piece.def.special == SpecialType.Etourdie && mc.requiresRecoil)
                {
                    var (r1, r2) = RecoilSquares(piece, mc);
                    occ[piece.x, piece.y] = null;
                    // previousPos = case d'avant (restera l'origine)
                    piece.x = r2.x; piece.y = r2.y;
                    piece.transform.position = new Vector3(piece.x * board.cellSize, piece.y * board.cellSize, 0);
                    occ[piece.x, piece.y] = piece;
                }

                // Voleur : tenter le vol
                if (piece.def.special == SpecialType.Thief)
                    TryThiefResurrect(piece);

                // Jumelles : appliquer le mouvement de la sœur si nécessaire
                ApplyPendingTwinMove();

                // Général : il s'est déplacé ? non (ici non), donc pas d'augmentation
                return;
            }

            // --- 3) Déplacement standard (simple ou capture non-squelette)
            occ[piece.x, piece.y] = null;
            piece.previousPos = new Vector2Int(piece.x, piece.y);
            piece.x = mc.to.x; piece.y = mc.to.y;
            piece.transform.position = new Vector3(piece.x * board.cellSize, piece.y * board.cellSize, 0);
            occ[piece.x, piece.y] = piece;

            // --- 4) Post-effets
            if (piece.def.special == SpecialType.Thief && mc.isCapture)
                TryThiefResurrect(piece);

            if (piece.def.special == SpecialType.Etourdie && mc.isCapture && mc.requiresRecoil)
            {
                var (r1, r2) = RecoilSquaresFromOldPos(origin, mc.to);
                occ[piece.x, piece.y] = null;
                // previousPos reste = origin (avant tout mouvement de ce tour)
                piece.x = r2.x; piece.y = r2.y;
                piece.transform.position = new Vector3(piece.x * board.cellSize, piece.y * board.cellSize, 0);
                occ[piece.x, piece.y] = piece;
            }

            // Jumelles : appliquer le mouvement de la sœur après le coup principal
            ApplyPendingTwinMove();

            // Général : +1 portée après un coup joué
            if (piece.def.special == SpecialType.General)
            {
                piece.generalRange = Mathf.Clamp(piece.generalRange + 1, 1, 8);
            }
        }

        void ApplyPendingTwinMove()
        {
            if (!pendingTwinMove.HasValue) return;

            var (sis, dest) = pendingTwinMove.Value;

            // Si la sœur a été capturée par l’explosion d’un squelette, abandonne
            if (sis == null) { pendingTwinMove = null; return; }

            // capture éventuelle pour la sœur (indépendante du premier mouvement)
            var target = occ[dest.x, dest.y];
            if (target != null && target.team != sis.team)
            {
                lastCapturedDef = target.def; lastCapturedTeam = target.team;
                target.OnCaptured(occ, this, new Vector2Int(dest.x, dest.y));
                Destroy(target.gameObject);
                occ[dest.x, dest.y] = null;
            }

            occ[sis.x, sis.y] = null;
            sis.previousPos = new Vector2Int(sis.x, sis.y);
            sis.x = dest.x; sis.y = dest.y;
            sis.transform.position = new Vector3(sis.x * board.cellSize, sis.y * board.cellSize, 0);
            occ[sis.x, sis.y] = sis;

            pendingTwinMove = null;
        }

        // -------------------- Pouvoir du Voleur --------------------

        void TryThiefResurrect(Piece thief)
        {
            if (lastCapturedDef == null) return;

            Vector2Int origin = thief.originalPos;
            if (!board.InBounds(origin.x, origin.y)) return;
            if (occ[origin.x, origin.y] != null) return; // doit être vide

            var go = new GameObject($"{lastCapturedDef.displayName}_{thief.team}_Stolen");
            go.transform.parent = board.transform;
            var p = go.AddComponent<Piece>();
            p.Init(lastCapturedDef, thief.team, origin.x, origin.y, board);
            occ[origin.x, origin.y] = p;

            lastCapturedDef = null;
        }

        // -------------------- Tour & fin de partie --------------------

        void EndTurn()
        {
            HighlightManager.I?.Clear();
            currentMoves.Clear();
            selected = null;

            currentTurn = (currentTurn == Team.White) ? Team.Black : Team.White;
            CheckGameEnd();
        }

        bool HasAnyLegalMove(Team team)
        {
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    var p = occ[x, y];
                    if (p == null || p.team != team) continue;
                    if (GetLegalMoves(p).Count > 0) return true;
                }
            return false;
        }

        bool IsInCheck(Team team)
        {
            var snap = Rules.Snapshot(occ);
            return Rules.IsKingInCheck(board, snap, team);
        }

        void CheckGameEnd()
        {
            Team side = currentTurn; // c'est au "side" de jouer maintenant
            Team opp = (side == Team.White) ? Team.Black : Team.White;

            bool any = HasAnyLegalMove(side);
            bool check = IsInCheck(side);

            if (!any && check)
            {
                Debug.Log($"Échec et mat ! {(opp == Team.White ? "Blancs" : "Noirs")} gagnent.");
                // TODO : afficher UI de fin + bouton retour menu
            }
            else if (!any && !check)
            {
                Debug.Log("Pat ! Match nul.");
                // TODO : UI de fin
            }
        }
    }
}
