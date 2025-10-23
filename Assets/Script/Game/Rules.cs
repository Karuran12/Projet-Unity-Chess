using System.Collections.Generic;
using UnityEngine;

namespace ChessDeck
{
    // Version légère d'une pièce pour la simulation
    public class SimPiece
    {
        public PieceDefinition def;
        public Team team;
        public Vector2Int pos;
        public Vector2Int originalPos; // pour le Voleur
        public bool alive = true;

        public SimPiece(Piece p)
        {
            def = p.def; team = p.team; pos = new Vector2Int(p.x, p.y);
            originalPos = p.originalPos;
        }
    }

    public static class Rules
    {
        // Copie logique du plateau à partir de occ[8,8]
        public static (SimPiece[,] grid, List<SimPiece> all) Snapshot(Piece[,] occ)
        {
            var grid = new SimPiece[8,8];
            var list = new List<SimPiece>();
            for (int y=0;y<8;y++)
            for (int x=0;x<8;x++)
            {
                var p = occ[x,y];
                if (p==null) continue;
                var sp = new SimPiece(p);
                grid[x,y] = sp; list.Add(sp);
            }
            return (grid, list);
        }

        // Applique un MoveCandidate sur la copie logique (gère les 4 pièces spéciales)
        public static void ApplyMoveLogic(BoardManager board, (SimPiece[,] grid, List<SimPiece> all) snap,
                                          Piece moverUnity, MoveCandidate mc)
        {
            var grid = snap.grid;
            // Récupère la pièce logique correspondante
            var mover = grid[moverUnity.x, moverUnity.y];
            if (mover == null || !mover.alive) return;

            // --- Archer : tir à distance (ne bouge pas), on supprime juste la cible si ennemie
            if (mover.def.special == SpecialType.Archer && mc.isRangedCapture)
            {
                var t = grid[mc.to.x, mc.to.y];
                if (t != null && t.team != mover.team) { t.alive = false; grid[mc.to.x, mc.to.y] = null; }
                return;
            }

            // --- Capture au contact : traite la cible (Squelette possible)
            bool capturedSkeleton = false;
            SimPiece captured = null;
            if (mc.isCapture)
            {
                captured = grid[mc.to.x, mc.to.y];
                if (captured != null && captured.team != mover.team)
                {
                    if (captured.def.special == SpecialType.SkeletonGiant) capturedSkeleton = true;

                    // Explosion du Squelette (case centrale intacte)
                    if (capturedSkeleton)
                    {
                        for (int dy=-1; dy<=1; dy++)
                        for (int dx=-1; dx<=1; dx++)
                        {
                            if (dx==0 && dy==0) continue;
                            int cx = mc.to.x + dx, cy = mc.to.y + dy;
                            if (!board.InBounds(cx,cy)) continue;
                            var around = grid[cx,cy];
                            if (around != null) { around.alive = false; grid[cx,cy] = null; }
                        }
                        // détruit la cible elle-même
                        captured.alive = false; grid[mc.to.x, mc.to.y] = null;

                        // L'attaquant NE prend PAS la case
                        // Étourdie : recule si requis
                        if (mover.def.special == SpecialType.Etourdie && mc.requiresRecoil)
                        {
                            var (r1,r2) = RecoilFrom(mover.pos, mc.to);
                            // on suppose r1/r2 libres car validés quand le coup a été généré
                            grid[mover.pos.x, mover.pos.y] = null;
                            mover.pos = r2; grid[mover.pos.x, mover.pos.y] = mover;
                        }

                        // Voleur : peut ressusciter la pièce capturée s'il a sa case d'origine libre
                        if (mover.def.special == SpecialType.Thief)
                            TryThiefResurrect(board, grid, mover, captured);

                        return;
                    }
                    else
                    {
                        // capture normale : on retire la cible, et on avancera l'attaquant après
                        captured.alive = false; grid[mc.to.x, mc.to.y] = null;
                    }
                }
            }

            // --- Déplacement standard / capture simple (non-squelette)
            grid[mover.pos.x, mover.pos.y] = null;
            mover.pos = mc.to;
            grid[mover.pos.x, mover.pos.y] = mover;

            // Étourdie : si capture, recule de 2 cases
            if (mover.def.special == SpecialType.Etourdie && mc.isCapture && mc.requiresRecoil)
            {
                var (r1, r2) = RecoilFrom(mc.to, mc.to); // direction basée sur le coup (arrivée)
                // ici on veut reculer à partir de la direction attaque : calcule depuis l'origine :
                (r1, r2) = RecoilFrom(mc.to, moverUnity ? new Vector2Int(moverUnity.x, moverUnity.y) : mc.to);
                grid[mover.pos.x, mover.pos.y] = null;
                mover.pos = r2; grid[mover.pos.x, mover.pos.y] = mover;
            }

            // Voleur : après capture simple, ressuscite la pièce capturée si possible
            if (mover.def.special == SpecialType.Thief && mc.isCapture && captured != null)
                TryThiefResurrect(board, grid, mover, captured);
        }

        static (Vector2Int r1, Vector2Int r2) RecoilFrom(Vector2Int to, Vector2Int from)
        {
            int dx = Mathf.Clamp(to.x - from.x, -1, 1);
            int dy = Mathf.Clamp(to.y - from.y, -1, 1);
            var r1 = new Vector2Int(to.x - dx, to.y - dy);
            var r2 = new Vector2Int(to.x - 2*dx, to.y - 2*dy);
            return (r1, r2);
        }

        static void TryThiefResurrect(BoardManager board, SimPiece[,] grid, SimPiece thief, SimPiece captured)
        {
            var origin = thief.originalPos;
            if (!board.InBounds(origin.x, origin.y)) return;
            if (grid[origin.x, origin.y] != null) return; // doit être vide
            if (captured == null || captured.def == null) return;

            var revived = new SimPiece(new DummyPiece(captured.def, thief.team, origin, thief.originalPos));
            grid[origin.x, origin.y] = revived;
        }

        // ----- Contrôle "roi en échec ?" -----

        public static bool IsKingInCheck(BoardManager board, (SimPiece[,] grid, List<SimPiece> all) snap, Team kingTeam)
        {
            Vector2Int kingPos = new Vector2Int(-1, -1);
            foreach (var sp in snap.all)
            {
                if (!sp.alive) continue;
                if (sp.team == kingTeam && sp.def.isKing) { kingPos = sp.pos; break; }
            }
            if (kingPos.x < 0) return true; // roi inexistant -> considéré "en échec" (perdant)

            // Génère toutes les cases attaquées par l'adversaire
            foreach (var sp in snap.all)
            {
                if (!sp.alive || sp.team == kingTeam) continue;
                foreach (var target in GenerateAttacks(board, snap.grid, sp))
                {
                    if (target == kingPos) return true;
                }
            }
            return false;
        }

        // Génère les cases attaquées par une pièce logique (version simplifiée de Piece.GetMoves)
        static IEnumerable<Vector2Int> GenerateAttacks(BoardManager board, SimPiece[,] grid, SimPiece sp)
        {
            // Archer : n'attaque qu'en tir avant (1..3) ; pas d'attaque au contact
            if (sp.def.special == SpecialType.Archer)
            {
                int forward = (sp.team == Team.White) ? 1 : -1;
                for (int step=1; step<=3; step++)
                {
                    int cx = sp.pos.x, cy = sp.pos.y + forward*step;
                    if (!board.InBounds(cx, cy)) break;
                    var hit = grid[cx, cy];
                    if (hit != null)
                    {
                        if (hit.team != sp.team) yield return new Vector2Int(cx, cy);
                        yield break;
                    }
                }
                yield break;
            }

            // Roi-like
            if (sp.def.isKing || (sp.def.maxRange==1 && sp.def.dirN && sp.def.dirS && sp.def.dirE && sp.def.dirW && sp.def.dirNE && sp.def.dirNW && sp.def.dirSE && sp.def.dirSW))
            {
                for (int dy=-1; dy<=1; dy++)
                for (int dx=-1; dx<=1; dx++)
                {
                    if (dx==0 && dy==0) continue;
                    int cx = sp.pos.x + dx, cy = sp.pos.y + dy;
                    if (!board.InBounds(cx,cy)) continue;
                    var hit = grid[cx, cy];
                    if (hit==null || hit.team != sp.team) yield return new Vector2Int(cx, cy);
                }
                yield break;
            }

            // Pion (captures diagonales uniquement)
            if (sp.def.isPawn)
            {
                int dir = (sp.team==Team.White)?1:-1;
                foreach (int dx in new int[]{-1,1})
                {
                    int cx = sp.pos.x + dx, cy = sp.pos.y + dir;
                    if (!board.InBounds(cx,cy)) continue;
                    yield return new Vector2Int(cx, cy);
                }
                yield break;
            }

            // Étourdie (= tour pour l'attaque), Voleur (= step ortho), et autres slides/steps
            if (sp.def.moveKind == MoveKind.Step)
            {
                if (sp.def.stepOffsets != null)
                foreach (var off in sp.def.stepOffsets)
                {
                    int cx = sp.pos.x + off.x, cy = sp.pos.y + off.y;
                    if (!board.InBounds(cx, cy)) continue;
                    var hit = grid[cx, cy];
                    if (hit==null || hit.team != sp.team) yield return new Vector2Int(cx, cy);
                }
                yield break;
            }

            // Slide
            var dirs = new List<Vector2Int>();
            if (sp.def.dirN)  dirs.Add(new Vector2Int(0,1));
            if (sp.def.dirS)  dirs.Add(new Vector2Int(0,-1));
            if (sp.def.dirE)  dirs.Add(new Vector2Int(1,0));
            if (sp.def.dirW)  dirs.Add(new Vector2Int(-1,0));
            if (sp.def.dirNE) dirs.Add(new Vector2Int(1,1));
            if (sp.def.dirNW) dirs.Add(new Vector2Int(-1,1));
            if (sp.def.dirSE) dirs.Add(new Vector2Int(1,-1));
            if (sp.def.dirSW) dirs.Add(new Vector2Int(-1,-1));

            foreach (var d in dirs)
            {
                int cx = sp.pos.x + d.x, cy = sp.pos.y + d.y;
                int step = 1;
                while (board.InBounds(cx,cy) && step <= sp.def.maxRange)
                {
                    var hit = grid[cx, cy];
                    yield return new Vector2Int(cx, cy);
                    if (hit != null) break;
                    cx += d.x; cy += d.y; step++;
                }
            }
        }
    }

    // utilitaire pour créer un SimPiece pour le voleur ressuscité
    class DummyPiece : Piece
    {
        public DummyPiece(PieceDefinition d, Team t, Vector2Int pos, Vector2Int orig)
        {
            def = d; team = t; x = pos.x; y = pos.y; originalPos = orig;
        }
    }
}
