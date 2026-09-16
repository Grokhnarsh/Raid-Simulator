using EmberDepths.Core.Grid;
using NUnit.Framework;
using UnityEngine;

namespace EmberDepths.Tests
{
    /// <summary>
    /// The projection is the one piece of maths everything else trusts. If a
    /// cell and its world position disagree by even half a tile, actors stand
    /// beside the floor and nothing above this layer can compensate.
    /// </summary>
    public sealed class IsoGridTests
    {
        [Test]
        public void CellToWorld_RoundTrips_ForEveryCellInARegion()
        {
            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    var cell = new GridCoord(x, y);
                    GridCoord back = IsoGrid.WorldToCell(IsoGrid.CellToWorld(cell));
                    Assert.AreEqual(cell, back, $"round trip failed for {cell}");
                }
            }
        }

        [Test]
        public void CellToWorld_MatchesUnityIsometricGridFormula()
        {
            // Unity's Grid in Isometric mode computes:
            //   x = (cx - cy) * cellSize.x * 0.5
            //   y = (cx + cy) * cellSize.y * 0.5
            // Tiles are placed by the tilemap using that, and actors by us using
            // IsoGrid. They must be the same function or the two drift apart.
            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    Vector3 ours = IsoGrid.CellToWorld(new GridCoord(x, y));
                    float unityX = (x - y) * IsoGrid.UnityCellSize.x * 0.5f;
                    float unityY = (x + y) * IsoGrid.UnityCellSize.y * 0.5f;

                    Assert.AreEqual(unityX, ours.x, 1e-4f);
                    Assert.AreEqual(unityY, ours.y, 1e-4f);
                }
            }
        }

        [Test]
        public void TileIs_TwoToOne()
        {
            Assert.AreEqual(IsoGrid.TileWidthPx, IsoGrid.TileHeightPx * 2,
                "the whole pipeline, Blender camera included, assumes a 2:1 tile");
        }

        [Test]
        public void NeighbourTable_IsIndexedByDirection()
        {
            // AllNeighbours[(int)dir] must be the step that moves that way on
            // screen. Several systems index it directly.
            for (int i = 0; i < 8; i++)
            {
                var direction = (IsoDirection)i;
                GridCoord step = GridCoord.Step(direction);
                IsoDirection derived = IsoDirectionExtensions.FromGridStep(GridCoord.Zero, step);

                Assert.AreEqual(direction, derived,
                    $"{direction} maps to step {step}, which reads as {derived}");
            }
        }

        [Test]
        public void DirectionAssetKeys_RoundTrip()
        {
            // The importer maps PixelLab's folder names onto this enum by string.
            for (int i = 0; i < 8; i++)
            {
                var direction = (IsoDirection)i;
                Assert.IsTrue(IsoDirectionExtensions.TryParseAssetKey(direction.ToAssetKey(), out IsoDirection parsed));
                Assert.AreEqual(direction, parsed);
            }
        }

        [Test]
        public void DepthKey_IncreasesAwayFromCamera()
        {
            // Larger x+y is further up the screen, which must sort behind.
            Assert.Less(IsoGrid.DepthKey(new GridCoord(0, 0)), IsoGrid.DepthKey(new GridCoord(1, 1)));
            Assert.Less(IsoGrid.DepthKey(new GridCoord(2, 3)), IsoGrid.DepthKey(new GridCoord(4, 4)));
        }
    }
}
