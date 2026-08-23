using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Procedural layout generator that carves non-overlapping rectangular rooms and connects
/// them with L-shaped corridors. The whole map is a solid wall to start, then rooms are
/// hollowed out and joined so every room is reachable. Fully deterministic for a given
/// seed. This is the first "real" implementation behind <see cref="IDungeonLayoutGenerator"/>.
/// </summary>
public sealed class RoomsAndCorridorsLayoutGenerator : IDungeonLayoutGenerator
{
    private readonly int _maxRooms;
    private readonly int _minRoomSize;
    private readonly int _maxRoomSize;

    public RoomsAndCorridorsLayoutGenerator(int maxRooms = 12, int minRoomSize = 4, int maxRoomSize = 9)
    {
        _maxRooms = maxRooms;
        _minRoomSize = minRoomSize;
        _maxRoomSize = maxRoomSize;
    }

    public TileMap Generate(int width, int height, int seed)
    {
        var tiles = new TileMap(width, height);
        tiles.Fill(TileKind.Wall);

        var rng = new Random(seed);
        var rooms = new List<Room>();
        Point? previousCenter = null;

        // Small maps can only fit so many rooms; cap the target so we still make progress.
        var targetRooms = Math.Min(_maxRooms, Math.Max(1, (width * height) / 90));

        for (int attempt = 0; attempt < targetRooms * 5 && rooms.Count < targetRooms; attempt++)
        {
            var room = TryPlaceRoom(rng, width, height, rooms);
            if (room is null)
            {
                continue;
            }

            rooms.Add(room.Value);
            CarveRoom(tiles, room.Value);

            if (previousCenter is Point prev)
            {
                CarveCorridor(tiles, prev, room.Value.Center, rng);
            }
            previousCenter = room.Value.Center;
        }

        return tiles;
    }

    private Room? TryPlaceRoom(Random rng, int mapWidth, int mapHeight, List<Room> existing)
    {
        var roomWidth = rng.Next(_minRoomSize, _maxRoomSize + 1);
        var roomHeight = rng.Next(_minRoomSize, _maxRoomSize + 1);

        // Leave a one-cell border so rooms never touch the map edge (guaranteeing wall
        // perimeter) and are no larger than the usable interior.
        if (roomWidth + 2 > mapWidth || roomHeight + 2 > mapHeight)
        {
            return null;
        }

        var x = rng.Next(1, mapWidth - roomWidth - 1);
        var y = rng.Next(1, mapHeight - roomHeight - 1);

        var candidate = new Room(x, y, roomWidth, roomHeight);

        // Reject rooms that touch or overlap an existing room (1-cell padding keeps a wall
        // between adjacent rooms so corridors are the only connective tissue).
        foreach (var room in existing)
        {
            if (candidate.Intersects(room))
            {
                return null;
            }
        }

        return candidate;
    }

    private static void CarveRoom(TileMap tiles, Room room)
    {
        for (int y = room.Y; y < room.Y + room.Height; y++)
        {
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                tiles.SetTile(x, y, TileKind.Floor);
            }
        }
    }

    private static void CarveCorridor(TileMap tiles, Point from, Point to, Random rng)
    {
        // L-shaped: horizontal leg then vertical leg, or the reverse, chosen at random.
        if (rng.Next(2) == 0)
        {
            CarveHorizontal(tiles, from.X, to.X, from.Y);
            CarveVertical(tiles, from.Y, to.Y, to.X);
        }
        else
        {
            CarveVertical(tiles, from.Y, to.Y, from.X);
            CarveHorizontal(tiles, from.X, to.X, to.Y);
        }
    }

    private static void CarveHorizontal(TileMap tiles, int x1, int x2, int y)
    {
        var min = Math.Min(x1, x2);
        var max = Math.Max(x1, x2);
        for (int x = min; x <= max; x++)
        {
            tiles.SetTile(x, y, TileKind.Floor);
        }
    }

    private static void CarveVertical(TileMap tiles, int y1, int y2, int x)
    {
        var min = Math.Min(y1, y2);
        var max = Math.Max(y1, y2);
        for (int y = min; y <= max; y++)
        {
            tiles.SetTile(x, y, TileKind.Floor);
        }
    }

    private readonly record struct Room(int X, int Y, int Width, int Height)
    {
        public Point Center => new(X + Width / 2, Y + Height / 2);

        /// <summary>
        /// True if this room overlaps (or touches) the other room. Padding by one cell
        /// ensures a wall separates adjacent rooms.
        /// </summary>
        public bool Intersects(Room other) =>
            X - 1 < other.X + other.Width &&
            X + Width + 1 > other.X &&
            Y - 1 < other.Y + other.Height &&
            Y + Height + 1 > other.Y;
    }
}
