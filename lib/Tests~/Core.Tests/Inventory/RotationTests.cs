using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using Xunit;

namespace ProjectSurvive.Core.Tests.Inventory
{
    /// <summary>
    /// Stage 05 task 2: a footprint is a set of cells, rotation is 0/90/180/270 and an
    /// asymmetric shape has to come back normalised.
    /// </summary>
    public sealed class RotationTests
    {
        /// <summary>Every bag and item id in the shipped data.</summary>
        public static IEnumerable<object[]> EveryPiece()
        {
            IGameConfig config = InventoryFixture.Config();
            foreach (BagConfig bag in config.Bags)
            {
                yield return new object[] { bag.Id, bag.Footprint };
            }

            foreach (ItemConfig item in config.Items)
            {
                yield return new object[] { item.Id, item.Footprint };
            }
        }

        [Theory]
        [MemberData(nameof(EveryPiece))]
        public void Four_clockwise_turns_return_the_authored_shape(string id, Footprint shape)
        {
            Footprint turned = shape;
            for (int i = 0; i < 4; i++)
            {
                turned = FootprintRotation.Rotate(turned, Rotation.Degrees90);
            }

            Assert.True(shape.CellCount > 0, id + " has no shape");
            Assert.Equal(shape.CellCount, turned.CellCount);
            Assert.Equal(shape.Width, turned.Width);
            Assert.Equal(shape.Height, turned.Height);
            for (int i = 0; i < shape.Cells.Count; i++)
            {
                Assert.True(shape.Cells[i] == turned.Cells[i], id + " cell " + i + " moved");
            }
        }

        [Theory]
        [MemberData(nameof(EveryPiece))]
        public void Half_turn_is_the_same_shape_twice_around(string id, Footprint shape)
        {
            Footprint once = FootprintRotation.Rotate(shape, Rotation.Degrees180);
            Footprint twice = FootprintRotation.Rotate(once, Rotation.Degrees180);

            Assert.True(shape.CellCount > 0, id + " has no shape");
            Assert.Equal(shape.CellCount, once.CellCount);
            Assert.Equal(shape.Cells.Count, twice.Cells.Count);
            for (int i = 0; i < shape.Cells.Count; i++)
            {
                Assert.True(shape.Cells[i] == twice.Cells[i], id + " cell " + i + " moved");
            }
        }

        [Fact]
        public void Asymmetric_bag_turns_into_a_different_shape()
        {
            // bag.wicker_basket_l is the L piece "X.|X.|XX" (Stage 04's required
            // asymmetric shape), so a quarter turn has to move cells.
            Footprint authored = ShapeOf("bag.wicker_basket_l");
            Footprint quarter = FootprintRotation.Rotate(authored, Rotation.Degrees90);
            Footprint half = FootprintRotation.Rotate(authored, Rotation.Degrees180);
            Footprint threeQuarter = FootprintRotation.Rotate(authored, Rotation.Degrees270);

            Assert.NotEqual(Cells(authored), Cells(quarter));
            Assert.NotEqual(Cells(authored), Cells(half));
            Assert.NotEqual(Cells(quarter), Cells(threeQuarter));

            // Normalised: the 90-degree turn of a 3-row x 2-column piece is 3 columns x 2 rows.
            Assert.Equal(3, quarter.Width);
            Assert.Equal(2, quarter.Height);
            Assert.Equal(4, quarter.CellCount);
        }

        [Fact]
        public void Asymmetric_turn_is_clockwise_in_board_axes()
        {
            // "X.|X.|XX" rotated 90 degrees clockwise is the top row full plus the
            // left cell of the second row: "XXX|X..".
            Footprint turned = FootprintRotation.Rotate(ShapeOf("bag.wicker_basket_l"), Rotation.Degrees90);

            Assert.Equal(3, turned.Width);
            Assert.Equal(2, turned.Height);
            Assert.Contains(new CellOffset(0, 0), turned.Cells);
            Assert.Contains(new CellOffset(1, 0), turned.Cells);
            Assert.Contains(new CellOffset(2, 0), turned.Cells);
            Assert.Contains(new CellOffset(0, 1), turned.Cells);
        }

        [Fact]
        public void Symmetric_bag_keeps_one_shape_at_every_orientation()
        {
            Footprint authored = ShapeOf("bag.raw_cotton_bag");

            foreach (Rotation rotation in new[]
                     {
                         Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270,
                     })
            {
                Footprint turned = FootprintRotation.Rotate(authored, rotation);
                Assert.Equal(9, turned.CellCount);
                Assert.Equal(3, turned.Width);
                Assert.Equal(3, turned.Height);
                Assert.Equal(Cells(authored), Cells(turned));
            }
        }

        [Theory]
        [MemberData(nameof(EveryPiece))]
        public void Turned_footprints_stay_normalised(string id, Footprint shape)
        {
            foreach (Rotation rotation in new[]
                     {
                         Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270,
                     })
            {
                Footprint turned = FootprintRotation.Rotate(shape, rotation);

                int minX = int.MaxValue;
                int minY = int.MaxValue;
                foreach (CellOffset cell in turned.Cells)
                {
                    Assert.True(cell.X >= 0 && cell.Y >= 0, "cell " + cell + " of " + id + " is not normalised");
                    minX = cell.X < minX ? cell.X : minX;
                    minY = cell.Y < minY ? cell.Y : minY;
                }

                Assert.Equal(0, minX);
                Assert.Equal(0, minY);
                Assert.Equal(shape.CellCount, turned.CellCount);
            }
        }

        [Fact]
        public void Footprint_from_cells_normalises_and_collapses_duplicates()
        {
            var cells = new List<CellOffset>
            {
                new CellOffset(5, 3),
                new CellOffset(5, 3),
                new CellOffset(6, 4),
            };

            Footprint footprint = Footprint.FromCells(cells);

            Assert.Equal(2, footprint.CellCount);
            Assert.Equal(2, footprint.Width);
            Assert.Equal(2, footprint.Height);
            Assert.Contains(new CellOffset(0, 0), footprint.Cells);
            Assert.Contains(new CellOffset(1, 1), footprint.Cells);
            Assert.True(Footprint.FromCells(new List<CellOffset>()).IsEmpty);
            Assert.True(Footprint.FromCells(null).IsEmpty);
        }

        [Fact]
        public void Quarter_turn_helpers_normalise_out_of_range_values()
        {
            Assert.Equal(Rotation.Degrees0, RotationOps.FromQuarterTurns(0));
            Assert.Equal(Rotation.Degrees90, RotationOps.FromQuarterTurns(1));
            Assert.Equal(Rotation.Degrees180, RotationOps.FromQuarterTurns(-2));
            Assert.Equal(Rotation.Degrees270, RotationOps.FromQuarterTurns(-1));
            Assert.Equal(Rotation.Degrees90, RotationOps.FromQuarterTurns(5));
            Assert.Equal(90f, Rotation.Degrees90.Degrees());
            Assert.Equal(Rotation.Degrees0, Rotation.Degrees270.Clockwise());
        }

        private static Footprint ShapeOf(string id)
        {
            IGameConfig config = InventoryFixture.Config();
            if (config.TryGetBag(id, out BagConfig bag))
            {
                return bag.Footprint;
            }

            if (config.TryGetItem(id, out ItemConfig item))
            {
                return item.Footprint;
            }

            throw new System.InvalidOperationException("fixture expects a piece '" + id + "'");
        }

        private static List<string> Cells(Footprint footprint)
        {
            var cells = new List<string>();
            foreach (CellOffset cell in footprint.Cells)
            {
                cells.Add(cell.ToString());
            }

            return cells;
        }
    }
}
