using System.Collections.Generic;
using UnityEngine;

namespace ChessDeck
{
    public enum Team { White, Black }

    // -------- Représentation d’un coup potentiel --------
    public struct MoveCandidate
    {
        public Vector2Int to;        // case cible (toujours sur l’échiquier)
        public bool isCapture;       // capture au contact ?
        public bool isRangedCapture; // capture à distance (Archer) — la pièce ne bouge pas
        public bool requiresRecoil;  // l’Étourdie doit reculer après capture
        public Vector2Int recoilTo;  // position finale du recul si applicable

        public MoveCandidate(Vector2Int to, bool isCapture = false)
        {
            this.to = to;
            this.isCapture = isCapture;
            this.isRangedCapture = false;
            this.requiresRecoil = false;
            this.recoilTo = Vector2Int.zero;
        }
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public class Piece : MonoBehaviour
    {
        // -------- Données statiques --------
        public PieceDefinition def;
        public Team team;

        // -------- État plateau --------
        public int x, y;                         // position actuelle (grid)
        public Vector2Int previousPos;           // position d’avant (utile pour Serpent)
        public Vector2Int originalPos;           // case de départ (Voleur)
        public int generalRange = 1;             // portée dynamique du Général (1..8)

        // Jumelles
        public int pairId = 0;                   // identifiant de duo
        public bool twinLost = false;            // si la sœur est morte, se comporte comme un cavalier normal

        // Refs
        [HideInInspector] public BoardManager board;
        SpriteRenderer sr;

        // ---------- Initialisation ----------
        public void Init(PieceDefinition d, Team t, int gx, int gy, BoardManager boardRef)
        {
            def = d;
            team = t;
            x = gx; y = gy;
            previousPos = new Vector2Int(x, y);
            originalPos = new Vector2Int(x, y); // par défaut (utile au Voleur)
            board = boardRef;

            if (def.special == SpecialType.General) generalRange = 1;

            sr = GetComponent<SpriteRenderer>();
            if (!sr) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = def.sprite;
            sr.sortingOrder = 1;

            transform.position = new Vector3(x * board.cellSize, y * board.cellSize, 0);
        }

        // ---------- Génération des coups ----------
        public IEnumerable<MoveCandidate> GetMoves(Piece[,] occ)
        {
            // ARCHER : mouvement roi (sans capture au contact) + tirs vers l’avant (0..3)
            if (def.special == SpecialType.Archer)
            {
                foreach (var m in KingLikeMoves(occ, allowContactCapture: false))
                    if (IsInsideBackZone(m.to)) yield return m;

                foreach (var shot in ArcherRangedShots(occ))
                    yield return shot;
                yield break;
            }

            // JUMELLES : se comportent comme un cavalier, validation “mouvement miroir” faite par GameManager
            if (def.special == SpecialType.Twins && !twinLost)
            {
                foreach (var m in KnightMoves(occ)) yield return m;
                yield break;
            }

            // SERPENT : fou à portée limitée (maxRange=3 dans l’asset)
            if (def.special == SpecialType.Serpent)
            {
                foreach (var m in SlideMoves(occ, 3)) yield return m;
                yield break;
            }

            // GÉNÉRAL : slide toutes directions avec portée dynamique
            if (def.special == SpecialType.General)
            {
                int r = Mathf.Clamp(generalRange, 1, 8);
                foreach (var m in SlideMoves(occ, r)) yield return m;
                yield break;
            }

            // COMMANDANTE : mouvement court défini dans l’asset (toutes directions, maxRange=2)
            // (Son aura sera appliquée côté règles/filtrage si on l’active)

            // PION (classique, côté déplacement — captures diagonales gérées ici)
            if (def.isPawn)
            {
                foreach (var m in PawnMoves(occ)) yield return m;
                yield break;
            }

            // ROI (1 case toutes directions)
            if (def.isKing)
            {
                foreach (var m in KingLikeMoves(occ, allowContactCapture: true)) yield return m;
                yield break;
            }

            // STEP (déplacements par offsets)
            if (def.moveKind == MoveKind.Step)
            {
                foreach (var m in StepMoves(occ)) yield return m;
                yield break;
            }

            // SLIDE (directions + portée)
            foreach (var m in SlideMoves(occ, def.maxRange)) yield return m;
        }

        // ---------- Mouvements de base ----------

        IEnumerable<MoveCandidate> StepMoves(Piece[,] occ)
        {
            if (def.stepOffsets == null) yield break;

            foreach (var off in def.stepOffsets)
            {
                int cx = x + off.x, cy = y + off.y;
                if (!board.InBounds(cx, cy)) continue;

                var target = occ[cx, cy];
                if (target == null)
                {
                    yield return new MoveCandidate(new Vector2Int(cx, cy), false);
                }
                else if (target.team != team)
                {
                    yield return new MoveCandidate(new Vector2Int(cx, cy), true);
                }
            }
        }

        IEnumerable<MoveCandidate> KnightMoves(Piece[,] occ)
        {
            Vector2Int[] offs = new Vector2Int[] {
                new Vector2Int(1,2), new Vector2Int(2,1), new Vector2Int(-1,2), new Vector2Int(-2,1),
                new Vector2Int(1,-2), new Vector2Int(2,-1), new Vector2Int(-1,-2), new Vector2Int(-2,-1)
            };
            foreach (var off in offs)
            {
                int cx = x + off.x, cy = y + off.y;
                if (!board.InBounds(cx, cy)) continue;

                var t = occ[cx, cy];
                if (t == null) yield return new MoveCandidate(new Vector2Int(cx, cy), false);
                else if (t.team != team) yield return new MoveCandidate(new Vector2Int(cx, cy), true);
            }
        }

        IEnumerable<MoveCandidate> SlideMoves(Piece[,] occ, int maxRange)
        {
            var dirs = new List<Vector2Int>();
            if (def.dirN)  dirs.Add(new Vector2Int(0, 1));
            if (def.dirS)  dirs.Add(new Vector2Int(0, -1));
            if (def.dirE)  dirs.Add(new Vector2Int(1, 0));
            if (def.dirW)  dirs.Add(new Vector2Int(-1, 0));
            if (def.dirNE) dirs.Add(new Vector2Int(1, 1));
            if (def.dirNW) dirs.Add(new Vector2Int(-1, 1));
            if (def.dirSE) dirs.Add(new Vector2Int(1, -1));
            if (def.dirSW) dirs.Add(new Vector2Int(-1, -1));

            foreach (var d in dirs)
            {
                int step = 1;
                int cx = x + d.x, cy = y + d.y;

                while (board.InBounds(cx, cy) && step <= maxRange)
                {
                    var target = occ[cx, cy];

                    if (target == null)
                    {
                        yield return new MoveCandidate(new Vector2Int(cx, cy), false);
                    }
                    else
                    {
                        if (target.team != team)
                        {
                            // Étourdie : ne peut PAS capturer les pions + doit reculer si capture
                            if (def.special == SpecialType.Etourdie)
                            {
                                if (target.def.isPawn) break; // interdit

                                // Deux cases derrière doivent être libres
                                var r1 = new Vector2Int(x - d.x, y - d.y);
                                var r2 = new Vector2Int(x - 2 * d.x, y - 2 * d.y);
                                if (board.InBounds(r1.x, r1.y) && board.InBounds(r2.x, r2.y)
                                    && occ[r1.x, r1.y] == null && occ[r2.x, r2.y] == null)
                                {
                                    var mc = new MoveCandidate(new Vector2Int(cx, cy), true)
                                    {
                                        requiresRecoil = true,
                                        recoilTo = r2
                                    };
                                    yield return mc;
                                }
                                break;
                            }

                            // Capture standard
                            yield return new MoveCandidate(new Vector2Int(cx, cy), true);
                        }
                        break; // bloqué après la première pièce rencontrée
                    }

                    cx += d.x; cy += d.y; step++;
                }
            }
        }

        IEnumerable<MoveCandidate> KingLikeMoves(Piece[,] occ, bool allowContactCapture)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int cx = x + dx, cy = y + dy;
                if (!board.InBounds(cx, cy)) continue;

                var t = occ[cx, cy];
                if (t == null) yield return new MoveCandidate(new Vector2Int(cx, cy), false);
                else if (t.team != team && allowContactCapture)
                    yield return new MoveCandidate(new Vector2Int(cx, cy), true);
            }
        }

        IEnumerable<MoveCandidate> PawnMoves(Piece[,] occ)
        {
            int dir = (team == Team.White) ? 1 : -1;

            // Avance d’1
            int nx = x, ny = y + dir;
            if (board.InBounds(nx, ny) && occ[nx, ny] == null)
                yield return new MoveCandidate(new Vector2Int(nx, ny), false);

            // Avance de 2 depuis rangée initiale
            int startRow = (team == Team.White) ? 1 : 6;
            if (y == startRow && board.InBounds(nx, ny) && occ[nx, ny] == null)
            {
                int ny2 = y + 2 * dir;
                if (board.InBounds(nx, ny2) && occ[nx, ny2] == null)
                    yield return new MoveCandidate(new Vector2Int(nx, ny2), false);
            }

            // Captures diagonales
            foreach (int dx in new int[] { -1, 1 })
            {
                int cx = x + dx, cy = y + dir;
                if (!board.InBounds(cx, cy)) continue;

                var t = occ[cx, cy];
                if (t != null && t.team != team)
                    yield return new MoveCandidate(new Vector2Int(cx, cy), true);
            }

            // (en passant/promotion non gérés pour la v1)
        }

        // ---------- Pouvoirs spéciaux ----------

        // Archer : tir à distance vers l’avant (jusqu’à 3 cases), bloqué par la 1re pièce rencontrée
        IEnumerable<MoveCandidate> ArcherRangedShots(Piece[,] occ)
        {
            int forward = (team == Team.White) ? 1 : -1;
            for (int step = 1; step <= 3; step++)
            {
                int cx = x, cy = y + forward * step;
                if (!board.InBounds(cx, cy)) break;

                var p = occ[cx, cy];
                if (p == null) continue; // traverse le vide
                // rencontre une pièce
                if (p.team != team)
                {
                    var mc = new MoveCandidate(new Vector2Int(cx, cy), true) { isRangedCapture = true };
                    yield return mc;
                }
                yield break; // bloqué par la première pièce rencontrée (alliée ou ennemie)
            }
        }

        // Archer : doit rester dans les 2 rangées de son camp
        bool IsInsideBackZone(Vector2Int target)
        {
            if (team == Team.White) return target.y <= 1;
            return target.y >= 6;
        }

        // Appelé par GameManager quand cette pièce est capturée
        public void OnCaptured(Piece[,] occ, GameManager gm, Vector2Int at)
        {
            if (def.special != SpecialType.SkeletonGiant) return;

            // Explosion : détruit toutes les pièces sur les 8 cases adjacentes (case centrale intacte)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue; // la case "at" reste intacte (mais sera vide)
                int cx = at.x + dx, cy = at.y + dy;
                if (!gm.board.InBounds(cx, cy)) continue;

                var p = occ[cx, cy];
                if (p != null)
                {
                    Object.Destroy(p.gameObject);
                    occ[cx, cy] = null;
                }
            }
        }
    }
}
